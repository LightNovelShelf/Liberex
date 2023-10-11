using Liberex.BackgroundServices;
using Liberex.Models.Context;
using Liberex.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/xhtml+xml");
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var dataDirectory = builder.Configuration["DataDirectory"] ?? "./";

builder.Services.AddDbContext<LiberexContext>(options =>
{
    options.UseSqlite($"DataSource={Path.Combine(dataDirectory, "database.db")}");
});

builder.Services.AddMemoryCache();
builder.Services.AddHostedService<FileMonitorService>();

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
