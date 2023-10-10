using Liberex.Internal;
using Liberex.Models.Context;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Wuyu.Epub;

namespace Liberex.BackgroundServices;

public class FileMonitorService : BackgroundService
{
    private readonly ILogger<FileMonitorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public FileMonitorService(ILogger<FileMonitorService> logger, IServiceProvider serviceProvider, IConfiguration configuration)
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
            await context.Librarys.AddAsync(new Library { FullPath = library[0], LibraryId = CorrelationIdGenerator.GetNextId() });

            var stopWatch = new Stopwatch();
            foreach (var item in Directory.EnumerateFiles(library[0], "*.epub"))
            {
                stopWatch.Restart();
                using var fileStream = new FileStream(item, FileMode.Open);
                var hash = await Utils.Hash.ComputeMD5Async(fileStream, stoppingToken);
                fileStream.Seek(0, SeekOrigin.Begin);
                Console.WriteLine(stopWatch.ElapsedMilliseconds);
                var epub = EpubBook.ReadEpub(fileStream, new MemoryStream());

                await context.Books.AddAsync(new Book
                {
                    BookId = CorrelationIdGenerator.GetNextId(),
                    Name = epub.Title,
                    Author = epub.Author,
                    FullPath = item,
                    Hash = hash
                });

                Console.WriteLine(stopWatch.ElapsedMilliseconds);
                // 需要改造一个完全只读，现在太耗性能
                epub.Dispose();

                _logger.LogInformation($"EPUB has be add: {Path.GetFileName(item)} ({stopWatch.ElapsedMilliseconds})");
            }

            await context.SaveChangesAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // do something

            await Task.Delay(5000, stoppingToken);
        }
    }
}