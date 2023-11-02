using Liberex.Internal;
using Liberex.Models.Context;
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

            var library = _configuration.GetSection("Library").Get<string[]>()!;
            await context.Librarys.AddAsync(new Library { FullPath = library[0], LibraryId = CorrelationIdGenerator.GetNextId() }, stoppingToken);

            foreach (var item in Directory.EnumerateFiles(library[0], "*.epub"))
            {
                using var fileStream = new FileStream(item, FileMode.Open);
                var hash = await Utils.Hash.ComputeMD5Async(fileStream, stoppingToken);
                fileStream.Seek(0, SeekOrigin.Begin);
                using var epub = await EpubBook.ReadEpubAsync(fileStream);

                var book = new Book
                {
                    BookId = "0HMUA15QG81O6",
                    Title = epub.Title,
                    Author = epub.Author,
                    FullPath = item,
                    Hash = hash
                };
                await context.Books.AddAsync(book, stoppingToken);
                await context.SaveChangesAsync(stoppingToken);

                _logger.LogInformation($"EPUB has be add: {Path.GetFileName(item)} ({book.BookId})");
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