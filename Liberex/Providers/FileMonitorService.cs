using Liberex.Internal;
using Liberex.Models.Context;
using Microsoft.EntityFrameworkCore;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Liberex.Providers;

public class FileMonitorService
{
    private readonly ILogger<FileMonitorService> _logger;
    private readonly LibraryService _libraryService;
    private readonly Subject<FileChangeData> _fileChangeSubject = new();
    private readonly Subject<LibraryChangeData> _libraryChangeSubject = new();
    private readonly Dictionary<string, WatcherData> _watchers = new();
    private readonly TaskQueue _taskQueue = new();

    public Subject<FileChangeData> FileChangeSubject => _fileChangeSubject;
    public Subject<LibraryChangeData> LibraryChangeSubject => _libraryChangeSubject;

    public FileMonitorService(ILogger<FileMonitorService> logger, IServiceProvider serviceProvider, IHostApplicationLifetime lifetime)
    {
        _logger = logger;

        var scoop = serviceProvider.CreateScope();
        var context = scoop.ServiceProvider.GetService<LiberexContext>();
        _libraryService = ActivatorUtilities.CreateInstance<LibraryService>(serviceProvider, context);

        _fileChangeSubject.GroupBy(e => e.FullPath)
            .SelectMany(g2 => g2.Throttle(TimeSpan.FromSeconds(3)))
            .Subscribe(x => _ = _taskQueue.Enqueue(() => FileChangeHandle(x, lifetime.ApplicationStopping)));
    }

    public async Task InitAsync(CancellationToken stoppingToken)
    {
        await _libraryService.InitAsync(stoppingToken);
        foreach (var item in _libraryService.Librarys) _ = Task.Run(() => WatchLibrary(item.Id, item.FullPath), stoppingToken);
    }

    private async ValueTask ScanLibraryAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var library = await _libraryService.GetLibraryByIdAsync(id, cancellationToken);
            // 扫描所有Series
            foreach (var item in Directory.EnumerateDirectories(library.FullPath, "*", SearchOption.TopDirectoryOnly))
            {
                // 检查 Series
                var series = await _libraryService.GetSeriesByPathAsync(item, cancellationToken);
                if (series is null)
                {
                    series = await _libraryService.AddSeriesAsync(library.Id, item, cancellationToken);
                    _libraryChangeSubject.OnNext(new LibraryChangeData(ChangeSource.Series, ChangeType.Add, series.Id, series.FullPath));
                }
                await ScanSeriesAsync(series, cancellationToken);
            }

            await CheckSeriesExistsAsync(id, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Scan library error: {id}", id);
        }
    }

    private async Task ScanSeriesAsync(Series series, CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var item in Directory.EnumerateFiles(series.FullPath, "*.epub", SearchOption.AllDirectories))
            {
                await ScanBookAsync(item, series, cancellationToken);
            }
            await CheckBooksExistsAsync(series.Id, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Scan series error: {Id}", series.Id);
        }
    }

    private async Task ScanBookAsync(string path, Series series, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _libraryService.Books.AnyAsync(x => x.FullPath == path, cancellationToken))
            {
                var book = await _libraryService.UpdateBookAsync(path, series, cancellationToken);
                if (book is not null) _libraryChangeSubject.OnNext(new LibraryChangeData(ChangeSource.Book, ChangeType.Update, book.Id, path));
            }
            else
            {
                var book = await _libraryService.AddBookAsync(path, series, cancellationToken);
                _libraryChangeSubject.OnNext(new LibraryChangeData(ChangeSource.Book, ChangeType.Add, book.Id, path));
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Scan book error: {Path}", path);
        }
    }

    private async ValueTask CheckSeriesExistsAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        // 标记不存在的Series
        var ids = new List<string>();
        foreach (var item in _libraryService.Series.Where(x => x.LibraryId == libraryId).Select(x => new { x.FullPath, x.Id }))
        {
            if (Directory.Exists(item.FullPath) == false)
            {
                ids.Add(item.Id);
                _libraryChangeSubject.OnNext(new LibraryChangeData(ChangeSource.Series, ChangeType.Delete, item.Id, item.FullPath));
            }
        }
        await _libraryService.SetSeriesDeleteByIdsAsync(ids, cancellationToken);
    }

    private async ValueTask CheckBooksExistsAsync(string seriesId, CancellationToken cancellationToken = default)
    {
        // 标记不存在的Book
        var ids = new List<string>();
        foreach (var item in _libraryService.Books.Where(x => x.SeriesId == seriesId).Select(x => new { x.FullPath, x.Id }))
        {
            if (File.Exists(item.FullPath) == false)
            {
                ids.Add(item.Id);
                _libraryChangeSubject.OnNext(new LibraryChangeData(ChangeSource.Book, ChangeType.Delete, item.Id, item.FullPath));
            }
        }
        await _libraryService.SetBookDeleteByIdsAsync(ids, cancellationToken);
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

    async Task FileChangeHandle(FileChangeData data, CancellationToken stoppingToken)
    {
        _logger.LogDebug("{ChangeType} Handle :{FullPath}", data.ChangeType, data.FullPath);

        try
        {
            // 仅出现在添加Library时
            if (data.LibraryPath == data.FullPath)
            {
                await ScanLibraryAsync(data.LibraryId, stoppingToken);
                return;
            }

            var series = await _libraryService.GetSeriesByPathAsync(data.FullPath, stoppingToken);

            if (data.ChangeType == WatcherChangeTypes.Deleted && series != null)
            {
                await _libraryService.SetSeriesDeleteByIdAsync(series.Id, stoppingToken);
                _libraryChangeSubject.OnNext(new LibraryChangeData(ChangeSource.Series, ChangeType.Delete, series.Id, series.FullPath));
            }
            else if (data.ChangeType == WatcherChangeTypes.Renamed)
            {
                if (series is not null) await _libraryService.RemoveSeriesAsync(series.Id, stoppingToken);
                series = await _libraryService.AddSeriesAsync(data.LibraryId, data.FullPath, stoppingToken);
                _libraryChangeSubject.OnNext(new LibraryChangeData(ChangeSource.Series, ChangeType.Add, series.Id, series.FullPath));
                await ScanSeriesAsync(series, stoppingToken);
            }
            else
            {
                if (series is null)
                {
                    series = await _libraryService.AddSeriesAsync(data.LibraryId, data.FullPath, stoppingToken);
                    _libraryChangeSubject.OnNext(new LibraryChangeData(ChangeSource.Series, ChangeType.Add, series.Id, series.FullPath));
                }
                await ScanSeriesAsync(series, stoppingToken);
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
                    if (isSeries) _fileChangeSubject.OnNext(new FileChangeData(id, path, args.ChangeType, seriesPath));
                    else _fileChangeSubject.OnNext(new FileChangeData(id, path, WatcherChangeTypes.Changed, seriesPath));
                }
                else
                {
                    _fileChangeSubject.OnNext(new FileChangeData(id, path, args.ChangeType, seriesPath));
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
            // watcher.Created += OnChanged;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            _logger.LogInformation("Start watch library: {Path}", path);
            _watchers[id] = new WatcherData(id, path, watcher);
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

public class WatcherData
{
    public string Id { get; }
    public string Path { get; }
    public FileSystemWatcher Watcher { get; }

    public WatcherData(string id, string path, FileSystemWatcher watcher)
    {
        Id = id;
        Path = path;
        Watcher = watcher;
    }
}

public class FileChangeData
{
    public string LibraryId { get; }
    public string LibraryPath { get; }

    public WatcherChangeTypes ChangeType { get; }
    public string FullPath { get; set; }

    public FileChangeData(string id, string libraryPath, WatcherChangeTypes changeType, string fullPath)
    {
        LibraryId = id;
        LibraryPath = libraryPath;
        ChangeType = changeType;
        FullPath = fullPath;
    }

    public FileChangeData(string id, string libraryPath, FileSystemEventArgs args)
    {
        LibraryId = id;
        LibraryPath = libraryPath;
        ChangeType = args.ChangeType;
        FullPath = args.FullPath;
    }

    public FileChangeData(Library library, WatcherChangeTypes changeType, string fullPath)
    {
        LibraryId = library.Id;
        LibraryPath = library.FullPath;
        ChangeType = changeType;
        FullPath = fullPath;
    }
}

public enum ChangeSource
{
    Library,
    Book,
    Series,
}

public enum ChangeType
{
    Add,
    Delete,
    Update,
}

public class LibraryChangeData
{
    public ChangeSource ChangeSource { get; }
    public ChangeType ChangeType { get; }
    public string Id { get; }
    public string Path { get; }

    public LibraryChangeData(ChangeSource changeSource, ChangeType changeType, string id, string path)
    {
        ChangeSource = changeSource;
        ChangeType = changeType;
        Id = id;
        Path = path;
    }
}