using Liberex.BackgroundServices;
using Liberex.Models.Context;
using Liberex.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var dataDirectory = builder.Configuration["DataDirectory"] ?? "./";

builder.Services.AddDbContext<LiberexContext>(options =>
{
    options.UseSqlite($"DataSource={Path.Combine(dataDirectory, "database.db")}");
});

builder.Services.AddHostedService<FileMonitorService>();
builder.Services.AddSingleton<FileScanService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
