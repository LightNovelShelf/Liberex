using Liberex.Models;
using Liberex.Models.Context;
using Liberex.Models.Subject;
using Liberex.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class SeriesController : ControllerBase
{
    private static readonly MessageModel s_seriesNotFound = MessageHelp.Error("Series not found", 404);

    private readonly ILogger<SeriesController> _logger;
    private readonly LibraryService _libraryService;
    private readonly FileMonitorService _fileMonitorService;

    public record BooksResult(Series Series, Pagination Page);

    public SeriesController(ILogger<SeriesController> logger, LibraryService libraryService, FileMonitorService fileMonitorService)
    {
        _logger = logger;
        _libraryService = libraryService;
        _fileMonitorService = fileMonitorService;
    }

    // 获取Series
    [HttpGet("{id}")]
    public async Task<ActionResult<MessageModel<Series>>> GetAsync(string id)
    {
        var series = await _libraryService.Series.SingleOrDefaultAsync(x => x.Id == id);
        if (series == null) return NotFound(s_seriesNotFound);
        return MessageHelp.Success(series);
    }

    // 获取Series下的Book列表，分页
    [HttpGet("{id}/[action]")]
    public async Task<ActionResult<MessageModel<BooksResult>>> BooksAsync(string id, int page = 1, int size = 20)
    {
        if (size <= 0 || size > 30) size = 20;
        var series = await _libraryService.Series.SingleOrDefaultAsync(x => x.Id == id);
        if (series == null) return NotFound(s_seriesNotFound);
        series.Books = await _libraryService.Books.OrderBy(x => x.Id)
            .Where(x => x.SeriesId == series.Id)
            .Skip(size * (page - 1))
            .Take(size)
            .ToArrayAsync();
        var total = await _libraryService.Books.CountAsync(x => x.SeriesId == series.Id);
        var totalPages = (int)Math.Ceiling(total / (double)size);
        return MessageHelp.Success(new BooksResult(series, new Pagination(page, total, totalPages)));
    }

    // 扫描
    [HttpGet("{id}/[action]")]
    public async Task<ActionResult<MessageModel>> ScanAsync(string id)
    {
        var series = await _libraryService.Series.SingleOrDefaultAsync(x => x.Id == id);
        if (series == null) return NotFound(s_seriesNotFound);
        _fileMonitorService.FileChangeSubject.OnNext(new FileChangeArgs(series.LibraryId, null, WatcherChangeTypes.Changed, series.FullPath));
        return MessageHelp.Success();
    }
}