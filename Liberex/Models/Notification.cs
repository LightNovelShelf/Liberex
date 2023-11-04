namespace Liberex.Models;

public class Notification
{
    private static int s_id = -1;

    public Notification()
    {
        Id = Interlocked.Add(ref s_id, 1);
    }

    public Notification(string type, object message) : this()
    {
        Type = type;
        Message = message;
    }

    public int Id { get; private set; }

    public object Message { get; set; }

    public string Type { get; set; }
}