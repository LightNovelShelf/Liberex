using Liberex.Providers;

namespace Liberex.HostServices;

public class FileMonitorHostService : IHostedService
{
    private readonly Task _completedTask = Task.CompletedTask;
    private readonly ILogger<FileMonitorHostService> _logger;
    private readonly FileMonitorService _fileMonitorService;

    public FileMonitorHostService(ILogger<FileMonitorHostService> logger, FileMonitorService fileMonitorService)
    {
        _logger = logger;
        _fileMonitorService = fileMonitorService;

        _fileMonitorService.LibraryChangeSubject.Subscribe((e) =>
        {
            _logger.LogInformation("[{ChangeSource}] {ChangeType} : {Path} ({Id})", e.ChangeSource, e.ChangeType, e.Path, e.Id ?? "null");
        });
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _fileMonitorService.InitAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _completedTask;
    }
}