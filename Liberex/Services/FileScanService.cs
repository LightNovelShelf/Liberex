using Liberex.Internal;
using Liberex.Models.Context;
using Microsoft.EntityFrameworkCore;
using Wuyu.Epub;

namespace Liberex.Services;

public class FileScanService
{
    private readonly ILogger<FileScanService> _logger;
    private readonly LiberexContext _context;

    private static int s_scaning = 0;
    private static int s_checking = 0;

    public FileScanService(ILogger<FileScanService> logger, LiberexContext context)
    {
        _logger = logger;
        _context = context;
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
        var library = await _context.Librarys.FirstOrDefaultAsync(x => x.LibraryId == id, cancellationToken)
                ?? throw new ScanException("no this library");
    }

    private async ValueTask ScanBySeriesAsync(string id, CancellationToken cancellationToken = default)
    {
        var series = await _context.Series.FirstOrDefaultAsync(x => x.SeriesId == id, cancellationToken)
            ?? throw new ScanException("no this series");

        foreach (var item in Directory.EnumerateFiles(series.FullPath, "*.epub", SearchOption.AllDirectories))
        {
            await AddBookAsync(item, series.LibraryId, series.SeriesId, cancellationToken);
        }
        // todo 还需要标记不存在的项目
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
            FullPath = fullPath,
            Hash = hash,
            SeriesId = seriesId,
            LibraryId = libraryId,

            // EPUB相关信息
            Title = epub.Title,
            Author = epub.Author,
            OEBPS = epub.OEBPS
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