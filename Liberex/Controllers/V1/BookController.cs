using Liberex.Models;
using Liberex.Models.Context;
using Liberex.Providers;
using Liberex.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Wuyu.Epub;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class BookController : ControllerBase
{
    private static readonly MessageModel s_bookNotFound = MessageHelp.Error("Book not found", 404);

    private readonly ILogger<BookController> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly LibraryService _libraryService;

    public BookController(ILogger<BookController> logger, LibraryService libraryService, IMemoryCache memoryCache)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _libraryService = libraryService;
    }

    private void PostEvictionCallback(object key, object value, EvictionReason reason, object state)
    {
        if (value is (EpubBook epub, SemaphoreSlim _))
        {
            epub.Dispose();
        }
    }

    private async ValueTask<(EpubBook, SemaphoreSlim)> GetEpubAsync(string id)
    {
        var result = _memoryCache.Get<(EpubBook, SemaphoreSlim)?>(id);
        if (result == null)
        {
            var fullPath = await _libraryService.Books
                .Where(x => x.Id == id)
                .Select(x => x.FullPath)
                .SingleOrDefaultAsync() ?? throw new FileNotFoundException("Book not found");
            var epub = await EpubBook.ReadEpubAsync(fullPath);
            var slim = new SemaphoreSlim(1, 1);

            var options = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                .RegisterPostEvictionCallback(PostEvictionCallback);

            result = (epub, slim);
            _memoryCache.Set(id, result, options);
        }
        return result.Value;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MessageModel<Book>>> GetAsync(string id)
    {
        var book = await _libraryService.Books.SingleOrDefaultAsync(x => x.Id == id);
        if (book == null) return NotFound(s_bookNotFound);
        return Ok(MessageHelp.Success(book));
    }

    [HttpGet("{id}/[action]")]
    public async Task<ActionResult<MessageModel<IEnumerable<string>>>> ItemAsync(string id)
    {
        try
        {
            var (epub, _) = await GetEpubAsync(id);
            var items = epub.GetTextIDs().Select(x => epub.GetItemById(x).Href);
            return MessageHelp.Success(items);
        }
        catch (FileNotFoundException)
        {
            return NotFound(s_bookNotFound);
        }
    }

    [HttpGet("{id}/[action]")]
    public async Task<ActionResult> ThumbnailAsync(string id)
    {
        var data = await _libraryService.GetBookThumbnailAsync(id);
        if (data == null) return NotFound(s_bookNotFound);
        return File(data, "image/jpeg");
    }

    [HttpGet("{id}/[action]/{**path}")]
    public async Task ItemAsync(string id, string path)
    {
        try
        {
            var (epub, slim) = await GetEpubAsync(id);

            await slim.WaitAsync();
            try
            {
                var item = epub.Package.Manifest.SingleOrDefault(x => x.Href == path);
                if (item == null)
                {
                    var result = NotFound(MessageHelp.Error("Item not found", 404));
                    await this.ExecuteResultAsync(result);
                }
                else
                {
                    using var stream = epub.GetItemStreamByID(item.ID);
                    var result = File(stream, item.MediaType);
                    await this.ExecuteResultAsync(result);
                }
            }
            finally
            {
                slim.Release();
            }
        }
        catch (FileNotFoundException)
        {
            await this.ExecuteResultAsync(NotFound(s_bookNotFound));
        }
    }
}