using Liberex.Models;

namespace Liberex.Providers.Event;

public class NotificationArgs : EventArgs
{
    public Notification Notification { get; }

    public NotificationArgs(Notification notification)
    {
        Notification = notification;
    }
}