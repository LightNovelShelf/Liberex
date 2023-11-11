using Liberex.Models;
using Liberex.Models.Context;
using Liberex.Providers;
using Liberex.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class LibraryController : ControllerBase
{
    private static readonly MessageModel s_libraryNotFound = MessageHelp.Error("Library not found", 404);

    private readonly ILogger<LibraryController> _logger;
    private readonly LiberexContext _context;
    private readonly FileMonitorService _fileMonitorService;

    public LibraryController(ILogger<LibraryController> logger, LiberexContext liberexContext, FileMonitorService fileMonitorService)
    {
        _logger = logger;
        _context = liberexContext;
        _fileMonitorService = fileMonitorService;
    }

    // 获取Library
    [HttpGet("{id}")]
    public async Task<ActionResult<MessageModel<Library>>> GetAsync(string id)
    {
        var library = await _context.Librarys.SingleOrDefaultAsync(x => x.Id == id);
        if (library == null) return NotFound(s_libraryNotFound);
        return MessageHelp.Success(library);
    }

    public record SeriesResult(Library Library, Pagination Page);

    // 获取Series列表，分页
    [HttpGet("{id}/[action]")]
    public async Task<ActionResult<MessageModel<SeriesResult>>> SeriesAsync(string id, int page = 1, int size = 20)
    {
        if (size <= 0 || size > 30) size = 20;
        var library = await _context.Librarys.SingleOrDefaultAsync(x => x.Id == id);
        if (library == null) return NotFound(s_libraryNotFound);
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

    // 删除库
    [HttpGet("{id}/[action]")]
    public async Task<ActionResult<MessageModel>> DeleteAsync(string id)
    {
        var count = await _context.Librarys.Where(x => x.Id == id).ExecuteDeleteAsync();
        if (count != 0) return MessageHelp.Success();
        else return NotFound(s_libraryNotFound);
    }

    // 扫描
    [HttpGet("{id}/[action]")]
    public async Task<ActionResult<MessageModel>> ScanAsync(string id)
    {
        var library = await _context.Librarys.SingleOrDefaultAsync(x => x.Id == id);
        if (library == null) return NotFound(s_libraryNotFound);
        _fileMonitorService.FileChangeSubject.OnNext(new FileChangeData(library, WatcherChangeTypes.Changed, library.FullPath));
        return MessageHelp.Success();
    }

    // 获取Library列表
    [HttpGet("[action]")]
    public async Task<MessageModel<Library[]>> ListAsync()
    {
        var librarys = await _context.Librarys.OrderBy(x => x.Id).ToArrayAsync();
        return MessageHelp.Success(librarys);
    }

    // 添加库
    [HttpGet("[action]")]
    public async Task<MessageModel<Library>> AddAsync([Required] string path, [Required] string name)
    {
        if (Directory.Exists(path) == false) return MessageHelp.Error<Library>("路径不存在");
        var library = new Library { Id = CorrelationIdGenerator.GetNextId(), FullPath = path, Name = name };
        await _context.Librarys.AddAsync(library);
        await _context.SaveChangesAsync();
        _fileMonitorService.WatchLibrary(library.Id, library.FullPath);
        _fileMonitorService.FileChangeSubject.OnNext(new FileChangeData(library, WatcherChangeTypes.Created, library.FullPath));
        return MessageHelp.Success(library);
    }

    // 清理被删除的数据
    [HttpGet("[action]")]
    public async Task<MessageModel> Clear()
    {
        await _context.Books.Where(x => x.IsDelete).ExecuteDeleteAsync();
        await _context.Series.Where(x => x.IsDelete).ExecuteDeleteAsync();

        return MessageHelp.Success();
    }
}