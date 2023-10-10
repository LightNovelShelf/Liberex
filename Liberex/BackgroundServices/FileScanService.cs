using Liberex.Internal;
using Liberex.Models.Context;
using Microsoft.EntityFrameworkCore;

namespace Liberex.BackgroundServices;

public class FileScanService : BackgroundService
{
    private readonly ILogger<FileScanService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public FileScanService(ILogger<FileScanService> logger, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetService<LiberexContext>()!;
        await context.Database.EnsureDeletedAsync(stoppingToken);

        if (await context.Database.EnsureCreatedAsync(stoppingToken))
        {
            _logger.LogInformation($"Database has Created");

            // to something
            var library = _configuration.GetSection("Library").Get<string[]>()!;
            await context.Librarys.AddAsync(new Library { AddTime = DateTime.Now, Path = library[0], LibraryId = CorrelationIdGenerator.GetNextId() });
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // do something

            await Task.Delay(5000, stoppingToken);
        }
    }
}