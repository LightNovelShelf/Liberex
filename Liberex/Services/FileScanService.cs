using Liberex.Models.Context;

namespace Liberex.Services;

public class FileScanService
{
    public async ValueTask Scan(string path, LiberexContext context)
    {
        await Task.CompletedTask;
    }
}