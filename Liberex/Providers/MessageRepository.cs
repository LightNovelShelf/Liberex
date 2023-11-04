using Liberex.Models;
using Liberex.Providers.Event;

namespace Liberex.Providers;

public interface IMessageRepository
{
    event EventHandler<NotificationArgs> NotificationEvent;
    void Broadcast(Notification notification);
}

public class MessageRepository : IMessageRepository
{
    private readonly ILogger<MessageRepository> _logger;

    public MessageRepository(ILogger<MessageRepository> logger)
    {
        _logger = logger;

        Task.Run(() =>
        {
            while (true)
            {
                NotificationEvent?.Invoke(this, new NotificationArgs(new Notification
                {
                    Type = "ping"
                }));
                Thread.Sleep(10 * 1000);
            }
        });
    }

    public event EventHandler<NotificationArgs> NotificationEvent;

    public void Broadcast(Notification notification)
    {
        _logger.LogDebug("Broadcasting event to all event listener");
        NotificationEvent?.Invoke(this, new NotificationArgs(notification));
    }
}