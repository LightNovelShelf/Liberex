namespace Liberex.Models;

public class MessageModel<T> : MessageModel
{
    public T Response { get; set; }
}

public class MessageModel
{
    public int Code { get; set; } = 0;

    public bool Success { get; set; } = true;

    public string Msg { get; set; } = string.Empty;
}

public static class MessageHelp
{
    public static MessageModel Success()
    {
        return new MessageModel() { Success = true };
    }

    public static MessageModel<T> Success<T>(T response)
    {
        return new MessageModel<T>() { Success = true, Response = response };
    }

    public static MessageModel Error(string message, int Code = 500)
    {
        return new MessageModel()
        {
            Success = false,
            Msg = message,
            Code = Code,
        };
    }

    public static MessageModel<T> Error<T>(string message, T response = default, int Code = 500)
    {
        return new MessageModel<T>()
        {
            Success = false,
            Msg = message,
            Code = Code,
            Response = response
        };
    }
}