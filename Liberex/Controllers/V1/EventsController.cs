using Liberex.Providers;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("sse/v1/[controller]")]
public class EventsController : ControllerBase
{
    private static int s_id = -1;

    private readonly ILogger<EventsController> _logger;
    private readonly FileMonitorService _fileMonitorService;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public EventsController(ILogger<EventsController> logger, FileMonitorService fileMonitorService, JsonSerializerOptions jsonSerializerOptions)
    {
        _logger = logger;
        _fileMonitorService = fileMonitorService;
        _jsonSerializerOptions = jsonSerializerOptions;
    }

    private void SetServerSentEventHeaders()
    {
        Response.StatusCode = 200;
        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Connection", "keep-alive");
    }

    private async Task WriteHeader(string eventType, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"id:{Interlocked.Add(ref s_id, 1)}\n", cancellationToken);
        await Response.WriteAsync($"event:{eventType}\n", cancellationToken);
    }

    private async Task CreatePingMessage(CancellationToken cancellationToken)
    {
        try
        {
            await WriteHeader("ping", cancellationToken);
            await Response.WriteAsync($"data:\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while sending message");
        }
    }

    private async Task CreateJsonMessage(string eventType, LibraryChangeData data, CancellationToken cancellationToken)
    {
        try
        {
            await WriteHeader(eventType, cancellationToken);
            var json = JsonSerializer.Serialize(data, _jsonSerializerOptions);
            await Response.WriteAsync($"data:{json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while sending message");
        }
    }

    [HttpGet]
    public async Task GetMessages(CancellationToken cancellationToken)
    {
        SetServerSentEventHeaders();

        var librarySubscribe = _fileMonitorService.LibraryChangeSubject
            .Subscribe(e => _ = CreateJsonMessage("library_change", e, cancellationToken));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10 * 1000, cancellationToken);
                await CreatePingMessage(cancellationToken);
            }
        }
        finally
        {
            librarySubscribe.Dispose();
        }
    }
}