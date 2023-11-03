using Liberex.Internal;
using Liberex.Models.Context;
using Liberex.Services;
using Microsoft.EntityFrameworkCore;
using Wuyu.Epub;

namespace Liberex.BackgroundServices;

public class FileMonitorService : BackgroundService
{
    private readonly ILogger<FileMonitorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly FileScanService _fileScanService;

    private bool _first = true;

    public FileMonitorService(ILogger<FileMonitorService> logger, IServiceProvider serviceProvider, IConfiguration configuration, FileScanService fileScanService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _fileScanService = fileScanService;
    }

    private async Task InitAsync(CancellationToken cancellationToken = default)
    {
        // 第一次启动时，初始化数据库
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetService<LiberexContext>();
        
        if (_first)
        {
            await context.Database.EnsureDeletedAsync(cancellationToken);
            await context.Database.EnsureCreatedAsync(cancellationToken);
            _first = false;
        }

        var librarySetting = _configuration.GetSection("Library").Get<string[]>();
        if (librarySetting is not null)
        {
            foreach (var item in librarySetting)
            {
                var library = await context.Librarys.FirstOrDefaultAsync(x => x.FullPath == item, cancellationToken);
                if (library is null)
                {
                    library = new Library { FullPath = item, Id = CorrelationIdGenerator.GetNextId() };
                    await context.Librarys.AddAsync(library, cancellationToken);
                    await context.SaveChangesAsync(cancellationToken);
                }
                await _fileScanService.ScanAsync(library.Id, null, cancellationToken);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await InitAsync(stoppingToken);
            await Task.Delay(60 * 1000, stoppingToken);
        }
    }
}
