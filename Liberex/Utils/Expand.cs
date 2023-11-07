namespace Liberex.Utils;

public static class Expand
{
    public async static Task Try(this Task task)
    {
        try
        {
            await task;
        }
        catch (Exception)
        {

        }
    }
}