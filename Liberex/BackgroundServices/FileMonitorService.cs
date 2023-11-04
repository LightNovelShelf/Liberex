using Liberex.Models.Context;
using Liberex.Services;

namespace Liberex.BackgroundServices;

public class FileMonitorService : BackgroundService
{
    private readonly ILogger<FileMonitorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly FileScanService _fileScanService;

    private bool _first = true;

    public FileMonitorService(ILogger<FileMonitorService> logger, IServiceProvider serviceProvider, IConfiguration configuration, FileScanService fileScanService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _fileScanService = fileScanService;
    }

    private async Task InitAsync(CancellationToken cancellationToken = default)
    {
        // 第一次启动时，初始化数据库
        if (_first)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetService<LiberexContext>();

            // 每次启动时清空数据库，重新创建
            // await context.Database.EnsureDeletedAsync(cancellationToken);
            await context.Database.EnsureCreatedAsync(cancellationToken);
            _first = false;
        }

        await _fileScanService.ScanAllAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await InitAsync(stoppingToken);
            }
            catch (Exception)
            {
            }

            // 半小时扫描一次
            await Task.Delay(30 * 60 * 1000, stoppingToken);
        }
    }
}
