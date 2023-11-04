using Liberex.Models;
using Liberex.Models.Context;
using Liberex.Services;
using Liberex.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class ListController : ControllerBase
{
    private readonly ILogger<ListController> _logger;
    private readonly LiberexContext _context;
    private readonly FileScanService _fileScanService;

    public ListController(ILogger<ListController> logger, LiberexContext liberexContext, FileScanService fileScanService)
    {
        _logger = logger;
        _context = liberexContext;
        _fileScanService = fileScanService;
    }

    public record BooksResult(Series Series, Pagination Page);

    // 获取Series下的Book列表，分页
    [HttpGet("[action]/{seriesId}")]
    public async ValueTask<MessageModel<BooksResult>> BooksAsync(string seriesId, int page = 1, int size = 20)
    {
        if (size <= 0 || size > 30) size = 20;
        var series = await _context.Series.SingleOrDefaultAsync(x => x.Id == seriesId);
        series.Books = await _context.Books.OrderBy(x => x.Id)
            .Where(x => x.SeriesId == series.Id)
            .Skip(size * (page - 1))
            .Take(size)
            .ToArrayAsync();
        var total = await _context.Books.CountAsync(x => x.SeriesId == series.Id);
        var totalPages = (int)Math.Ceiling(total / (double)size);
        return MessageHelp.Success(new BooksResult(series, new Pagination(page, total, totalPages)));
    }

    public record SeriesResult(Library Library, Pagination Page);

    // 获取Series列表，分页
    [HttpGet("[action]/{libraryId}")]
    public async ValueTask<MessageModel<SeriesResult>> SeriesAsync(string libraryId, int page = 1, int size = 20)
    {
        if (size <= 0 || size > 30) size = 20;
        var library = await _context.Librarys.SingleOrDefaultAsync(x => x.Id == libraryId);
        library.Series = await _context.Series
            .Where(x => x.LibraryId == library.Id)
            .OrderBy(x => x.Id)
            .Skip(size * (page - 1))
            .Take(size)
            .ToArrayAsync();
        // 为每个Series获取最新的一本Book
        foreach (var series in library.Series)
        {
            series.Books = await _context.Books
                .Where(x => x.SeriesId == series.Id)
                .OrderByDescending(x => x.AddTime)
                .Take(1)
                .ToArrayAsync();
        }
        var total = await _context.Series.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)size);
        return MessageHelp.Success(new SeriesResult(library, new Pagination(page, total, totalPages)));
    }

    // 获取Library列表
    [HttpGet("[action]")]
    public async ValueTask<MessageModel<Library[]>> LibrarysAsync()
    {
        var librarys = await _context.Librarys.OrderBy(x => x.Id).ToArrayAsync();
        return MessageHelp.Success(librarys);
    }

    /// <summary>
    /// 添加库
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    [HttpGet("[action]")]
    public async ValueTask<MessageModel<Library>> AddLibraryAsync(string path)
    {
        var library = new Library { Id = CorrelationIdGenerator.GetNextId(), FullPath = path };
        await _context.Librarys.AddAsync(library);
        await _context.SaveChangesAsync();
        await _fileScanService.ScanAsync(library.Id, null);
        return MessageHelp.Success(library);
    }

    // 删除库
    [HttpGet("[action]")]
    public async ValueTask<MessageModel> DeleteLibraryAsync(string id)
    {
        await _context.Librarys.Where(x => x.Id == id).ExecuteDeleteAsync();
        return MessageHelp.Success();
    }

    // 扫描
    [HttpGet("[action]")]
    public MessageModel ScanAsync(string libraryId, string seriesId)
    {
        Task.Run(() => _fileScanService.ScanAsync(libraryId, seriesId));
        return MessageHelp.Success();
    }

    // 清理被删除的数据
    [HttpGet("[action]")]
    public async ValueTask<MessageModel> Clear()
    {
        await _context.Books.Where(x => x.IsDelete).ExecuteDeleteAsync();
        await _context.Series.Where(x => x.IsDelete).ExecuteDeleteAsync();

        return MessageHelp.Success();
    }
}