using Liberex.Models;
using Liberex.Models.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class ListController : ControllerBase
{
    private readonly ILogger<ListController> _logger;
    private readonly LiberexContext _context;

    public ListController(ILogger<ListController> logger, LiberexContext liberexContext)
    {
        _logger = logger;
        _context = liberexContext;
    }

    // 获取Series下的Book列表，分页
    [HttpGet("[action]/{key}")]
    public async ValueTask<MessageModel> BooksAsync(string key, int page = 1, int size = 20)
    {
        if (size <= 0 || size > 30) size = 20;
        // TODO 后续只能传ID
        var series = await _context.Series.SingleOrDefaultAsync(x => x.Id == key || x.FullPath == key);
        series.Books = await _context.Books.OrderBy(x => x.Id)
            .Where(x => x.SeriesId == series.Id)
            .Skip(size * (page - 1))
            .Take(size)
            .ToArrayAsync();
        var total = await _context.Books.CountAsync(x => x.SeriesId == series.Id);
        var totalPages = (int)Math.Ceiling(total / (double)size);
        return MessageHelp.Success(new { series, page = new { total, totalPages, page } });
    }

    // 获取Series列表，分页
    [HttpGet("[action]/{key}")]
    public async ValueTask<MessageModel> SeriesAsync(string key, int page = 1, int size = 20)
    {
        if (size <= 0 || size > 30) size = 20;
        // TODO 后续只能传ID
        var library = await _context.Librarys.SingleOrDefaultAsync(x => x.Id == key || x.FullPath == key);
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
        return MessageHelp.Success(new { library, page = new { total, totalPages, page } });
    }

    // 获取Library列表
    [HttpGet("[action]")]
    public async ValueTask<MessageModel> LibrarysAsync()
    {
        var librarys = await _context.Librarys.OrderBy(x => x.Id).ToArrayAsync();
        return MessageHelp.Success(librarys);
    }
}