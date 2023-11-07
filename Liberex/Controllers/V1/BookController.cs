using Liberex.Models;
using Liberex.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Wuyu.Epub;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class BookController : ControllerBase
{
    private static readonly IDictionary<string, string> ContentTypeMappings = (new FileExtensionContentTypeProvider()).Mappings;

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

    [HttpGet("[action]/{id}")]
    public async ValueTask<ActionResult<MessageModel<IEnumerable<string>>>> ItemAsync(string id)
    {
        try
        {
            var (epub, _) = await GetEpubAsync(id);
            var items = epub.GetTextIDs().Select(x => epub.GetItemById(x).Href);
            return MessageHelp.Success(items);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("[action]/{id}/{**path}")]
    public async ValueTask ItemAsync(string id, string path)
    {
        try
        {
            var (epub, slim) = await GetEpubAsync(id);

            await slim.WaitAsync();
            try
            {
                var entry = epub.GetItemEntryByHref(path);
                if (entry == null)
                {
                    Response.StatusCode = 404;
                    return;
                }

                var extension = Path.GetExtension(path);
                using var stream = entry.Open();

                Response.ContentLength = entry.Length;
                var contentType = "application/octet-stream";
                if (extension.Equals(".opf"))
                {
                    contentType = "application/xml";
                }
                else
                {
                    if (ContentTypeMappings.TryGetValue(extension, out var value)) contentType = value;
                    Response.ContentType = contentType;
                }

                byte[] buffer = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(buffer)) != 0)
                {
                    await Response.BodyWriter.WriteAsync(buffer.AsMemory(0, read));
                }
            }
            finally
            {
                slim.Release();
            }
        }
        catch (FileNotFoundException)
        {
            Response.StatusCode = 404;
            return;
        }
    }
}