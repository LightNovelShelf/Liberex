using Liberex.Models;
using Liberex.Models.Context;
using Microsoft.AspNetCore.Mvc;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class BookController : ControllerBase
{
    private readonly ILogger<BookController> _logger;
    private readonly LiberexContext _context;

    public BookController(ILogger<BookController> logger, LiberexContext liberexContext)
    {
        _logger = logger;
        _context = liberexContext;
    }

    [HttpGet("[action]")]
    public MessageModel Items()
    {
        return MessageHelp.Success();
    }
}