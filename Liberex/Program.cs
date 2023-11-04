using Liberex.BackgroundServices;
using Liberex.Models.Context;
using Liberex.Providers;
using Liberex.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/xhtml+xml");
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var dataDirectory = builder.Configuration["DataDirectory"] ?? "./";
if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);
builder.Services.AddDbContext<LiberexContext>(options =>
{
    options.UseSqlite($"DataSource={Path.Combine(dataDirectory, "database.db")}");
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<FileScanService>();
builder.Services.AddHostedService<FileMonitorService>();
builder.Services.AddSingleton<IMessageRepository, MessageRepository>();

var app = builder.Build();

app.UseResponseCompression();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
