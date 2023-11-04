using Liberex.Models;
using Liberex.Models.Context;
using Liberex.Providers;
using Liberex.Providers.Event;
using Liberex.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liberex.Controllers.V1;

[ApiController]
[Route("sse/v1/[controller]")]
public class EventsController : ControllerBase
{
    private readonly ILogger<ListController> _logger;
    private readonly IMessageRepository _messageRepository;

    public EventsController(ILogger<ListController> logger, LiberexContext liberexContext, FileScanService fileScanService, IMessageRepository messageRepository)
    {
        _logger = logger;
        _messageRepository = messageRepository;
    }

    private void SetServerSentEventHeaders()
    {
        Response.StatusCode = 200;
        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Connection", "keep-alive");
    }

    [HttpGet]
    public async Task GetMessages(CancellationToken cancellationToken)
    {
        SetServerSentEventHeaders();

        async void onMessageCreated(object sender, NotificationArgs eventArgs)
        {
            try
            {
                await Response.WriteAsync($"id:{eventArgs.Notification.Id}\n", cancellationToken);
                await Response.WriteAsync($"event:{eventArgs.Notification.Type}\n", cancellationToken);
                if (eventArgs.Notification.Message is null)
                {
                    await Response.WriteAsync($"data:\n\n", cancellationToken);
                }
                else if (eventArgs.Notification.Message is string str)
                {
                    await Response.WriteAsync($"data:{str}\n\n", cancellationToken);
                }
                else
                {
                    var json = JsonSerializer.Serialize(eventArgs.Notification.Message);
                    await Response.WriteAsync($"data:{json}\n\n", cancellationToken);
                }

                await Response.Body.FlushAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while sending message");
            }
        }
        _messageRepository.NotificationEvent += onMessageCreated;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
        finally
        {
            _messageRepository.NotificationEvent -= onMessageCreated;
        }
    }
}