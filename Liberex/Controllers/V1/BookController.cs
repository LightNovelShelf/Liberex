using Liberex.Models;
using Liberex.Models.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Buffers;
using Wuyu.Epub;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class BookController : ControllerBase
{
    private static readonly IDictionary<string, string> ContentTypeMappings = (new FileExtensionContentTypeProvider()).Mappings;

    private readonly ILogger<BookController> _logger;
    private readonly LiberexContext _context;
    private readonly IMemoryCache _memoryCache;

    public BookController(ILogger<BookController> logger, LiberexContext liberexContext, IMemoryCache memoryCache)
    {
        _logger = logger;
        _context = liberexContext;
        _memoryCache = memoryCache;
    }

    private void PostEvictionCallback(object key, object? value, EvictionReason reason, object? state)
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
            var book = await _context.Books.Where(x => x.BookId == id).FirstAsync();
            var epub = await EpubBook.ReadEpubAsync(book.FullPath);
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
    public async ValueTask<MessageModel> ItemsAsync(string id)
    {
        var (epub, _) = await GetEpubAsync(id);
        var items = epub.GetTextIDs().ToArray();
        return MessageHelp.Success(items);
    }

    [HttpGet("[action]/{id}/{**path}")]
    public async ValueTask ItemAsync(string id, string path)
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
            Response.ContentType = ContentTypeMappings[extension] ?? "application/octet-stream";

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
}