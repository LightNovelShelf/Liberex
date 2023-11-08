using Liberex.Models;
using Microsoft.AspNetCore.Mvc;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class OSController : ControllerBase
{
    private readonly ILogger<OSController> _logger;

    public OSController(ILogger<OSController> logger)
    {
        _logger = logger;
    }

    [HttpGet("[action]")]
    public ActionResult<MessageModel<string[]>> Files(string path)
    {
        return MessageHelp.Success(Directory.GetFiles(path));
    }

    [HttpGet("[action]")]
    public ActionResult<MessageModel<string[]>> Directories(string path)
    {
        return MessageHelp.Success(Directory.GetDirectories(path));
    }

    public record FileEntry(string Path, bool IsDirectory);

    [HttpGet("[action]")]
    public ActionResult<MessageModel<FileEntry[]>> List(string path)
    {
        var list = new List<FileEntry>();
        foreach (var file in Directory.GetFiles(path)) list.Add(new FileEntry(file, false));
        foreach (var dir in Directory.GetDirectories(path)) list.Add(new FileEntry(dir, true));
        return MessageHelp.Success(list.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Path).ToArray());
    }
}