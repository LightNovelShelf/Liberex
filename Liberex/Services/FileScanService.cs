using Liberex.Internal;
using Liberex.Models.Context;
using Microsoft.EntityFrameworkCore;
using Wuyu.Epub;

namespace Liberex.Services;

public class FileScanService
{
    private readonly ILogger<FileScanService> _logger;
    private readonly LiberexContext _context;

    private static readonly SemaphoreSlim SLIM = new(1, 1);

    public FileScanService(ILogger<FileScanService> logger, LiberexContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async ValueTask ScanByLibraryAsync(string id, CancellationToken cancellationToken = default)
    {
        await SLIM.WaitAsync(cancellationToken);
        try
        {
            var library = await _context.Librarys.FirstOrDefaultAsync(x => x.LibraryId == id, cancellationToken)
                ?? throw new ScanException("no this library");

            // todo
        }
        finally
        {
            SLIM.Release();
        }
    }

    public async ValueTask ScanBySeriesAsync(string id, CancellationToken cancellationToken = default)
    {
        await SLIM.WaitAsync(cancellationToken);
        try
        {
            var series = await _context.Series.FirstOrDefaultAsync(x => x.SeriesId == id, cancellationToken)
                ?? throw new ScanException("no this series");

            foreach (var item in Directory.EnumerateFiles(series.FullPath, "*.epub", SearchOption.AllDirectories))
            {
                await AddBookAsync(item, series.LibraryId, series.SeriesId, cancellationToken);
            }
            // todo 还需要标记不存在的项目
        }
        finally
        {
            SLIM.Release();
        }
    }

    public async ValueTask AddBookAsync(string fullPath, string libraryId, string seriesId, CancellationToken cancellationToken = default)
    {
        using var fileStream = new FileStream(fullPath, FileMode.Open);
        var hash = await Utils.Hash.ComputeMD5Async(fileStream, cancellationToken);
        fileStream.Seek(0, SeekOrigin.Begin);
        using var epub = await EpubBook.ReadEpubAsync(fileStream);

        var book = new Book
        {
            BookId = CorrelationIdGenerator.GetNextId(),
            Name = epub.Title,
            Author = epub.Author,
            FullPath = fullPath,
            Hash = hash,
            SeriesId = seriesId,
            LibraryId = libraryId
        };
        // todo 可能已经存在
        await _context.Books.AddAsync(book, cancellationToken);

        _logger.LogInformation($"EPUB has be add: {Path.GetFileName(fullPath)} ({book.BookId})");
    }
}

public class ScanException : Exception
{
    public ScanException(string message) : base(message) { }
}