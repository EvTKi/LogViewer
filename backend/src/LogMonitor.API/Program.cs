using System.IO;
using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

using LogMonitor.Core.Configs;
using LogMonitor.Infrastructure.Data;
using LogMonitor.Infrastructure.Services;
try
{
// 1. Определяем путь к логам
var logDir = Path.Combine(Directory.GetCurrentDirectory(), "log");
Directory.CreateDirectory(logDir);

// 2. Создаём WebApplicationBuilder — ТОЛЬКО ОН даёт доступ к Configuration
var builder = WebApplication.CreateBuilder(args);

var localConfigPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.local.json");
if (File.Exists(localConfigPath))
{
    builder.Configuration.AddJsonFile(localConfigPath, optional: false, reloadOnChange: true);
}

// 3. Настраиваем Serilog СРАЗУ ПОСЛЕ builder
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
   // .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [{SourceContext}] - {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(logDir, "LogViewer_.log"),
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [{SourceContext}] - {Message:lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30
    )
    .CreateLogger();

// 4. Говорим хосту использовать Serilog
builder.Host.UseSerilog();


// 5. Далее — обычная настройка
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
// Console.WriteLine($"🔍 ConnectionString: '{conn}'");

builder.Services.AddDbContext<LogMonitorDbContext>(opt =>
    opt.UseNpgsql(conn));
// Телеграмм
builder.Services.Configure<TelegramOptions>(
    builder.Configuration.GetSection("Telegram"));
builder.Services.AddHostedService<TelegramPollingService>();

builder.Services.AddHttpClient(); // для IHttpClientFactory
builder.Services.AddSingleton<TelegramService>();

builder.Services.AddSingleton<LogMonitor.Core.Services.IErrorDetectionService, LogMonitor.Infrastructure.Services.ErrorDetectionService>();
builder.Services.AddSingleton<LogMonitor.Core.Services.IFileMonitoringService, LogMonitor.Infrastructure.Services.HybridFileWatcher>();
builder.Services.AddSingleton<LogMonitor.Core.Services.INotificationRouter, LogMonitor.API.Services.NotificationRouter>();
builder.Services.AddHostedService<LogMonitor.Infrastructure.BackgroundServices.LogMonitoringHostedService>();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LogMonitor API", Version = "v1" });
});

var app = builder.Build();

app.Logger.LogInformation("🔧 Приложение сконфигурировано. Запуск хоста...");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<LogMonitor.API.Hubs.ErrorNotificationHub>("/errorhub");


// === Проверка подключения к БД ===
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();
    await dbContext.Database.OpenConnectionAsync(); // Пробуем открыть соединение
    dbContext.Database.CloseConnection(); // Закрываем — EF откроет сам при необходимости
    app.Logger.LogInformation("✅ Успешное подключение к базе данных PostgreSQL.");
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "❌ Невозможно подключиться к базе данных. " +
        "Проверьте ConnectionString в appsettings.local.json:\n" +
        "    - Host, Port, Database\n" +
        "    - Username и Password\n" +
        "    - Доступность PostgreSQL сервера");
    
    // Завершаем приложение с кодом ошибки
    Environment.Exit(1);
}
app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("❗ FATAL ERROR: " + ex);
    throw;
}