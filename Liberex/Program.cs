using Liberex.HostServices;
using Liberex.Internal;
using Liberex.Models.Context;
using Liberex.Providers;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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

var settingPath = Path.Combine(dataDirectory, "setting.json");
if (!File.Exists(settingPath)) File.WriteAllText(settingPath, "{}");
builder.Configuration.AddJsonFile(settingPath, optional: true, reloadOnChange: true);
builder.Services.AddDbContext<LiberexContext>(options =>
{
    options.UseSqlite($"DataSource={Path.Combine(dataDirectory, "database.db")}").UseSnakeCaseNamingConvention();
});

var jsonSerializerOptions = new JsonSerializerOptions()
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    ReferenceHandler = ReferenceHandler.IgnoreCycles,
    PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance,
    Converters =
    {
        new JsonStringEnumConverter(SnakeCaseNamingPolicy.Instance)
    }
};

builder.Services.AddScoped<LibraryService>();
// 全局Json序列化配置
builder.Services.AddSingleton(jsonSerializerOptions);
builder.Services.AddMemoryCache();
// 文件监控服务
builder.Services.AddSingleton<FileMonitorService>();
builder.Services.AddHostedService<FileMonitorHostService>();

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
