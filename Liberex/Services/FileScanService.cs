using Liberex.Internal;
using Liberex.Models.Context;
using Microsoft.EntityFrameworkCore;
using Wuyu.Epub;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Liberex.Services;

public class FileScanService : IDisposable
{
    private readonly ILogger<FileScanService> _logger;
    private readonly LiberexContext _context;
    private readonly IServiceScope _scope;

    private static int s_scaning = 0;
    private static int s_checking = 0;
    private bool disposedValue;

    public FileScanService(ILogger<FileScanService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;

        _scope = serviceProvider.CreateScope();
        _context = _scope.ServiceProvider.GetService<LiberexContext>()!;
    }

    public async ValueTask ScanAsync(string libraryId, string seriesId, CancellationToken cancellationToken = default)
    {
        if (0 != Interlocked.Exchange(ref s_scaning, 1))
        {
            _logger.LogInformation("Scan has already start");
            return;
        }

        try
        {
            _logger.LogInformation("Scan has be start");

            if (!string.IsNullOrWhiteSpace(libraryId))
            {
                await ScanByLibraryAsync(libraryId, cancellationToken);
                return;
            }

            if (!string.IsNullOrWhiteSpace(seriesId))
            {
                await ScanBySeriesAsync(seriesId, cancellationToken);
                return;
            }
        }
        finally
        {
            _logger.LogInformation("Scan has be end");
            Interlocked.Exchange(ref s_scaning, 0);
        }
    }

    private async ValueTask ScanByLibraryAsync(string id, CancellationToken cancellationToken = default)
    {
        var library = await _context.Librarys.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new ScanException("no this library");
        // 扫描所有Series
        foreach (var item in Directory.EnumerateDirectories(library.FullPath, "*", SearchOption.TopDirectoryOnly))
        {
            // 检查 Series
            var series = await _context.Series.FirstOrDefaultAsync(x => x.FullPath == item, cancellationToken);
            if (series is null)
            {
                series = new Series
                {
                    Id = CorrelationIdGenerator.GetNextId(),
                    FullPath = item,
                    LibraryId = library.Id,
                };
                await _context.Series.AddAsync(series, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation($"Series has be add: {Path.GetFileName(series.FullPath)} ({series.Id})");
            }
            await ScanBySeriesAsync(series.Id, cancellationToken);
        }

        // 标记不存在的Series
        var ids = new List<string>();
        foreach (var item in _context.Series.Where(x => x.LibraryId == id).Select(x => new { x.FullPath, x.Id }))
        {
            if (!Directory.Exists(item.FullPath))
            {
                _logger.LogInformation($"Series has be remove: {Path.GetFileName(item.FullPath)} ({item.Id})");
                ids.Add(item.Id);
            }
        }
        await _context.Series
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsDelete, true), cancellationToken);
    }

    private async ValueTask ScanBySeriesAsync(string id, CancellationToken cancellationToken = default)
    {
        var series = await _context.Series.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ScanException("no this series");

        foreach (var item in Directory.EnumerateFiles(series.FullPath, "*.epub", SearchOption.AllDirectories))
        {
            await AddBookAsync(item, series, cancellationToken);
        }

        // 标记不存在的Book
        var ids = new List<string>();
        foreach (var item in _context.Books.Where(x => x.SeriesId == id).Select(x => new { x.FullPath, x.Id }))
        {
            if (!File.Exists(item.FullPath))
            {
                _logger.LogInformation($"EPUB has be remove: {Path.GetFileName(item.FullPath)} ({item.Id})");
                ids.Add(item.Id);
            }
        }
        await _context.Books
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsDelete, true), cancellationToken);
    }

    public async ValueTask AddBookAsync(string fullPath, Series series, CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(fullPath);

        async Task UpdateAsync(Book book)
        {
            using var fileStream = new FileStream(fullPath, FileMode.Open);
            var hash = await Utils.Hash.ComputeMD5Async(fileStream, cancellationToken);
            fileStream.Seek(0, SeekOrigin.Begin);
            using var epub = await EpubBook.ReadEpubAsync(fileStream);

            if (book.Hash != hash)
            {
                book.IsDelete = false;
                book.Hash = hash;
                book.ModifyTime = fileInfo.LastWriteTime;
                book.FileSize = fileInfo.Length;
                book.Title = epub.Title;
                book.Author = epub.Author;
                book.Opf = epub.Opf;

                series.LastUpdateTime = DateTime.Now;
            }
        }

        var bookQuery = _context.Books.Where(x => x.FullPath == fullPath);
        if (await bookQuery.AnyAsync(cancellationToken))
        {
            // 存在的情况，判断是否需要更新
            if (await bookQuery.AnyAsync(x => x.ModifyTime != fileInfo.LastWriteTime || x.FileSize != fileInfo.Length, cancellationToken))
            {
                var book = await bookQuery.FirstAsync(cancellationToken);
                await UpdateAsync(book);
                _logger.LogInformation($"EPUB has be update: {Path.GetFileName(fullPath)} ({series.Id})");
            }
        }
        else
        {
            var book = new Book
            {
                Id = CorrelationIdGenerator.GetNextId(),
                FullPath = fullPath,

                Series = series,
            };
            await UpdateAsync(book);
            await _context.Books.AddAsync(book, cancellationToken);
            _logger.LogInformation($"EPUB has be add: {Path.GetFileName(fullPath)} ({book.Id})");
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _scope.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public class ScanException : Exception
{
    public ScanException(string message) : base(message) { }
}