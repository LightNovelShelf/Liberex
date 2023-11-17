using Blurhash.ImageSharp;
using Liberex.Models.Context;
using Liberex.Utils;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.Formats.Jpeg;
using Wuyu.Epub;

namespace Liberex.Providers;

public class LibraryService
{
    public readonly LiberexContext _context;
    public readonly ILogger<LibraryService> _logger;

    public LibraryService(LiberexContext context, ILogger<LibraryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitAsync(CancellationToken cancellationToken = default)
    {
        // 每次启动时清空数据库，重新创建
        // await _context.Database.EnsureDeletedAsync(cancellationToken);
        await _context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    #region Library

    public IQueryable<Library> Librarys => _context.Librarys;

    public async ValueTask<Library> AddLibraryAsync(string path, string name, CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(path) == false) throw new DirectoryNotFoundException(path);
        var library = new Library { Id = CorrelationIdGenerator.GetNextId(), FullPath = path, Name = name };
        await _context.Librarys.AddAsync(library, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return library;
    }

    public async ValueTask<Library> GetLibraryByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.Librarys.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async ValueTask<Library> GetLibraryByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        return await _context.Librarys.SingleOrDefaultAsync(x => x.FullPath == path, cancellationToken);
    }

    public async ValueTask RemoveLibraryAsync(string id, CancellationToken cancellationToken = default)
    {
        await _context.Librarys.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    #endregion

    #region Series

    public IQueryable<Series> Series => _context.Series;

    public async ValueTask<Series> AddSeriesAsync(string libraryId, string path, CancellationToken cancellationToken = default)
    {
        var series = new Series
        {
            Id = CorrelationIdGenerator.GetNextId(),
            FullPath = path,
            LibraryId = libraryId,
        };
        await _context.Series.AddAsync(series, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return series;
    }

    public async ValueTask<Series> GetSeriesByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.Series.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async ValueTask<Series> GetSeriesByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        return await _context.Series.SingleOrDefaultAsync(x => x.FullPath == path, cancellationToken);
    }

    public async ValueTask RemoveSeriesAsync(string id, CancellationToken cancellationToken = default)
    {
        await _context.Series.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async ValueTask SetSeriesDeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await SetSeriesDeleteByIdsAsync(new string[] { id }, cancellationToken);
    }

    public async ValueTask SetSeriesDeleteByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Any() == false) return;
        await _context.Series
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsDelete, true).SetProperty(x => x.LastUpdateTime, DateTime.Now), cancellationToken);
        await _context.Books
            .Where(x => ids.Contains(x.SeriesId))
            .ExecuteUpdateAsync(x => x.SetProperty(x => x.IsDelete, true), cancellationToken);
    }

    #endregion

    #region Book

    public IQueryable<Book> Books => _context.Books;

    public async ValueTask<byte[]> GetBookThumbnailAsync(string id, CancellationToken cancellationToken = default)
    {
        var cover = await _context.BookCovers.SingleOrDefaultAsync(x => x.BookId == id, cancellationToken);
        return cover?.Thumbnail;
    }

    private async Task<bool> UpdateBookAsync(Book book, FileInfo fileInfo, CancellationToken cancellationToken = default)
    {
        using var fileStream = new FileStream(book.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await Hash.ComputeMD5Async(fileStream, cancellationToken);
        fileStream.Seek(0, SeekOrigin.Begin);
        using var epub = await EpubBook.ReadEpubAsync(fileStream);

        if (book.Hash != hash)
        {
            book.Cover ??= await _context.BookCovers.SingleOrDefaultAsync(x => x.BookId == book.Id, cancellationToken);

            book.IsDelete = false;
            book.Hash = hash;
            book.ModifyTime = fileInfo.LastWriteTime;
            book.FileSize = fileInfo.Length;
            book.Title = epub.Title;
            book.Author = epub.Author;
            book.Opf = epub.Opf;

            var data = await epub.GetItemDataByIDAsync(epub.Cover)
                ?? await epub.GetItemDataByIDAsync("cover")
                ?? await epub.GetItemDataByIDAsync("cover.jpg");
            if (data != null)
            {
                book.Cover ??= new BookCover { BookId = book.Id };
                book.Cover.Data = data;

                using var image = Image.Load<Rgba32>(data);
                book.Cover.Height = image.Height;
                book.Cover.Width = image.Width;
                // 有点耗性能。。。
                book.Cover.Placeholder = Blurhasher.Encode(image, 2, 3);

                // Resize image to 300 height
                using var stream = new MemoryStream();
                image.Mutate(x => x.Resize(image.Width * 300 / image.Height, 300));
                image.Metadata.ExifProfile = null;
                image.Metadata.XmpProfile = null;
                image.Metadata.IccProfile = null;
                image.Metadata.IptcProfile = null;
                await image.SaveAsync(stream, new JpegEncoder { Quality = 70 }, cancellationToken);
                book.Cover.Thumbnail = stream.ToArray();
            }

            return true;
        }

        return false;
    }

    public async ValueTask<Book> AddBookAsync(string fullPath, string seriesId, bool updateSeries = true, CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(fullPath);
        var book = new Book
        {
            Id = CorrelationIdGenerator.GetNextId(),
            FullPath = fullPath,

            Cover = new(),
            SeriesId = seriesId,
        };

        await UpdateBookAsync(book, fileInfo, cancellationToken);
        await _context.Books.AddAsync(book, cancellationToken);
        if (updateSeries)
        {
            await _context.Series
                .Where(x => x.Id == seriesId)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.LastUpdateTime, DateTime.Now), cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);

        return book;
    }

    public async ValueTask<bool> UpdateBookAsync(Book book, bool updateSeries = true, CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(book.FullPath);
        if (book.ModifyTime == fileInfo.LastWriteTime && book.FileSize == fileInfo.Length) return false;

        var hasUpdate = await UpdateBookAsync(book, fileInfo, cancellationToken);
        if (updateSeries)
        {
            await _context.Series
                .Where(x => x.Id == book.SeriesId)
                .ExecuteUpdateAsync(x => x.SetProperty(x => x.LastUpdateTime, DateTime.Now), cancellationToken);
        }
        if (hasUpdate) await _context.SaveChangesAsync(cancellationToken);

        return hasUpdate;
    }

    public async ValueTask SetBookDeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await _context.Books.Where(x => x.Id == id).ExecuteUpdateAsync(x => x.SetProperty(x => x.IsDelete, true), cancellationToken);
    }

    public async ValueTask SetBookDeleteByIdsAsync(IEnumerable<string> id, CancellationToken cancellationToken = default)
    {
        if (id.Any() == false) return;
        await _context.Books.Where(x => id.Contains(x.Id)).ExecuteUpdateAsync(x => x.SetProperty(x => x.IsDelete, true), cancellationToken);
    }

    public async ValueTask SetBookDeleteByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        await _context.Books.Where(x => x.FullPath == path).ExecuteUpdateAsync(x => x.SetProperty(x => x.IsDelete, true), cancellationToken);
    }

    #endregion
}