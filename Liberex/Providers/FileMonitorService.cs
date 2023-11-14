using Liberex.Internal;
using Liberex.Models.Context;
using Liberex.Models.Subject;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Writers;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Liberex.Providers;

public class FileMonitorService
{
    private readonly ILogger<FileMonitorService> _logger;
    private readonly IServiceProvider _serviceProvider;

    private readonly Subject<FileChangeArgs> _fileChangeSubject = new();
    private readonly Subject<LibraryChangeArgs> _libraryChangeSubject = new();
    private readonly Subject<SeriesUpdateArgs> _seriesUpdateSubject = new();
    private readonly Dictionary<string, WatcherItem> _watchers = new();

    public Subject<FileChangeArgs> FileChangeSubject => _fileChangeSubject;
    public Subject<LibraryChangeArgs> LibraryChangeSubject => _libraryChangeSubject;
    public Subject<SeriesUpdateArgs> SeriesUpdateSubject => _seriesUpdateSubject;

    public FileMonitorService(ILogger<FileMonitorService> logger, IServiceProvider serviceProvider, PriorityTaskQueue taskQueue)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        _fileChangeSubject.GroupBy(e => e.FullPath)
            .SelectMany(g2 => g2.Throttle(TimeSpan.FromSeconds(3)))
            .Subscribe(x => taskQueue.Write(async (token) => await FileChangeHandle(x, token), TaskPriority.Low));

        _seriesUpdateSubject.GroupBy(e => e.SeriesId)
            .SelectMany(g2 => g2.Throttle(TimeSpan.FromSeconds(10)))
            .Subscribe(data => taskQueue.Write(async (token) =>
            {
                var scope = _serviceProvider.CreateScope();
                var libraryService = scope.ServiceProvider.GetService<LibraryService>();
                await libraryService.Series
                .Where(s => s.Id == data.SeriesId)
                .ExecuteUpdateAsync(s => s.SetProperty(s => s.LastUpdateTime, data.UpdateTime), token);
            }, TaskPriority.Normal));
    }

    public async Task InitAsync(CancellationToken stoppingToken)
    {
        var scope = _serviceProvider.CreateScope();
        var libraryService = scope.ServiceProvider.GetService<LibraryService>();
        await libraryService.InitAsync(stoppingToken);
        foreach (var item in libraryService.Librarys) _ = Task.Run(() => WatchLibrary(item.Id, item.FullPath), stoppingToken);
    }

    private async ValueTask ScanLibraryAsync(LibraryService libraryService, string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var library = await libraryService.GetLibraryByIdAsync(id, cancellationToken);
            // 扫描所有Series
            foreach (var item in Directory.EnumerateDirectories(library.FullPath, "*", SearchOption.TopDirectoryOnly))
            {
                // 检查 Series
                var series = await libraryService.GetSeriesByPathAsync(item, cancellationToken);
                if (series is null)
                {
                    series = await libraryService.AddSeriesAsync(library.Id, item, cancellationToken);
                    _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Series, ChangeType.Add, series.Id, series.FullPath));
                }
                if (series.IsDelete)
                {
                    series.IsDelete = false;
                    series.LastUpdateTime = DateTime.Now;
                    await libraryService.SaveChangesAsync(cancellationToken);
                    _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Series, ChangeType.ReAdd, series.Id, series.FullPath));
                }
                await ScanSeriesAsync(libraryService, series, cancellationToken);
            }

            await CheckSeriesExistsAsync(libraryService, id, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Scan library error: {id}", id);
        }
    }

    private async Task ScanSeriesAsync(LibraryService libraryService, Series series, CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var item in Directory.EnumerateFiles(series.FullPath, "*.epub", SearchOption.AllDirectories))
            {
                await ScanBookAsync(libraryService, item, series.Id, cancellationToken);
            }
            await CheckBooksExistsAsync(libraryService, series.Id, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Scan series error: {Id}", series.Id);
        }
    }

    private async Task ScanBookAsync(LibraryService libraryService, string path, string seriesId, CancellationToken cancellationToken = default)
    {
        try
        {
            var book = libraryService.Books.SingleOrDefault(x => x.FullPath == path);
            if (book == null)
            {
                book = await libraryService.AddBookAsync(path, seriesId, false, cancellationToken);
                _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Book, ChangeType.Add, book.Id, path));
                _seriesUpdateSubject.OnNext(new SeriesUpdateArgs(seriesId, DateTime.Now));
            }
            else
            {
                var isDelete = book.IsDelete;
                book.IsDelete = false;
                var hasUpdate = await libraryService.UpdateBookAsync(book, false, cancellationToken);
                if (hasUpdate == false) await libraryService.SaveChangesAsync(cancellationToken);

                if (isDelete)
                {
                    _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Book, ChangeType.ReAdd, null, path));
                    _seriesUpdateSubject.OnNext(new SeriesUpdateArgs(seriesId, DateTime.Now));
                }
                if (hasUpdate)
                {
                    _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Book, ChangeType.Update, book.Id, path));
                    _seriesUpdateSubject.OnNext(new SeriesUpdateArgs(seriesId, DateTime.Now));
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Scan book error: {Path}", path);
        }
    }

    private async ValueTask CheckSeriesExistsAsync(LibraryService libraryService, string libraryId, CancellationToken cancellationToken = default)
    {
        // 标记不存在的Series
        var ids = new List<string>();
        foreach (var item in libraryService.Series.Where(x => x.LibraryId == libraryId).Select(x => new { x.FullPath, x.Id }))
        {
            if (Directory.Exists(item.FullPath) == false)
            {
                ids.Add(item.Id);
                _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Series, ChangeType.Delete, item.Id, item.FullPath));
            }
        }
        await libraryService.SetSeriesDeleteByIdsAsync(ids, cancellationToken);
    }

    private async ValueTask CheckBooksExistsAsync(LibraryService libraryService, string seriesId, CancellationToken cancellationToken = default)
    {
        // 标记不存在的Book
        var ids = new List<string>();
        foreach (var item in libraryService.Books.Where(x => x.SeriesId == seriesId).Select(x => new { x.FullPath, x.Id }))
        {
            if (File.Exists(item.FullPath) == false)
            {
                ids.Add(item.Id);
                _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Book, ChangeType.Delete, item.Id, item.FullPath));
            }
        }
        await libraryService.SetBookDeleteByIdsAsync(ids, cancellationToken);
    }

    private static bool GetSeriesPath(string path, string libraryPath, out string seriesPath)
    {
        var directory = new DirectoryInfo(path);
        if (directory.Parent.FullName == libraryPath)
        {
            seriesPath = directory.FullName;
            return true;
        }
        else
        {
            while (directory.Parent.FullName != libraryPath) directory = directory.Parent;
            seriesPath = directory.FullName;
            return false;
        }
    }

    async Task FileChangeHandle(FileChangeArgs data, CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ChangeType} Handle :{FullPath}", data.ChangeType, data.FullPath);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var libraryService = scope.ServiceProvider.GetService<LibraryService>();

            // 仅出现在添加Library时
            if (data.LibraryPath == data.FullPath)
            {
                await ScanLibraryAsync(libraryService, data.LibraryId, stoppingToken);
                return;
            }

            var series = await libraryService.GetSeriesByPathAsync(data.FullPath, stoppingToken);

            if (data.ChangeType == WatcherChangeTypes.Deleted && series != null)
            {
                await libraryService.SetSeriesDeleteByIdAsync(series.Id, stoppingToken);
                _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Series, ChangeType.Delete, series.Id, series.FullPath));
            }
            else if (data.ChangeType == WatcherChangeTypes.Renamed)
            {
                if (series is not null) await libraryService.RemoveSeriesAsync(series.Id, stoppingToken);
                series = await libraryService.AddSeriesAsync(data.LibraryId, data.FullPath, stoppingToken);
                _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Series, ChangeType.Add, series.Id, series.FullPath));
                await ScanSeriesAsync(libraryService, series, stoppingToken);
            }
            else
            {
                if (series is null)
                {
                    series = await libraryService.AddSeriesAsync(data.LibraryId, data.FullPath, stoppingToken);
                    _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Series, ChangeType.Add, series.Id, series.FullPath));
                }
                else if (series.IsDelete)
                {
                    series.IsDelete = false;
                    series.LastUpdateTime = DateTime.Now;
                    await libraryService.SaveChangesAsync();
                    _libraryChangeSubject.OnNext(new LibraryChangeArgs(ChangeSource.Series, ChangeType.ReAdd, series.Id, series.FullPath));
                }
                await ScanSeriesAsync(libraryService, series, stoppingToken);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Handle file change error: {FullPath}", data.FullPath);
        }
    }

    public void WatchLibrary(string id, string path)
    {
        try
        {
            void OnChanged(object sender, FileSystemEventArgs args)
            {
                // 删除的时候，出现Delete事件，以及父目录的Change事件
                // 修改的时候，出现Change事件
                // 创建的时候，出现Create事件, 以及父目录的Change事件
                // 上面如果父目录是被监控的目录，则无事件

                _logger.LogDebug("({ChangeType}) File was change: {FullPath}", args.ChangeType, args.FullPath);

                var isSeries = GetSeriesPath(args.FullPath, path, out var seriesPath);
                if (args.ChangeType == WatcherChangeTypes.Renamed || args.ChangeType == WatcherChangeTypes.Deleted)
                {
                    if (isSeries) _fileChangeSubject.OnNext(new FileChangeArgs(id, path, args.ChangeType, seriesPath));
                    else _fileChangeSubject.OnNext(new FileChangeArgs(id, path, WatcherChangeTypes.Changed, seriesPath));
                }
                else
                {
                    _fileChangeSubject.OnNext(new FileChangeArgs(id, path, args.ChangeType, seriesPath));
                }
            };

            FileSystemWatcher watcher = new(path)
            {
                NotifyFilter = NotifyFilters.CreationTime
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.FileName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
            };

            watcher.Changed += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnChanged;
            watcher.Created += OnChanged;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            _logger.LogInformation("Start watch library: {Path}", path);
            _watchers[id] = new WatcherItem(id, path, watcher);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Watch library error: {Path}", path);
        }
    }

    public void StopWatchLibrary(string id)
    {
        if (_watchers.TryGetValue(id, out var watcher))
        {
            watcher.Watcher.EnableRaisingEvents = false;
            watcher.Watcher.Dispose();
            _watchers.Remove(id);
        }
    }
}

public class WatcherItem
{
    public string Id { get; }
    public string Path { get; }
    public FileSystemWatcher Watcher { get; }

    public WatcherItem(string id, string path, FileSystemWatcher watcher)
    {
        Id = id;
        Path = path;
        Watcher = watcher;
    }
}