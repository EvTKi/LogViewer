using System.IO;
using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.NpgSql;

using LogMonitor.Core.Configs;
using LogMonitor.Infrastructure.Data;
using LogMonitor.Infrastructure.Services;


//TODO: прикрутить рассылку по email
try
{
// === CLI ARGUMENT PARSER (CUSTOM FORMAT) ===
var originalArgs = args.ToList();
var processedArgs = new List<string>();

// Извлекаем и обрабатываем --urls и --connstring
string? urlsOverride = null;
string? connStringOverride = null;

for (int i = 0; i < originalArgs.Count; i++)
{
    var arg = originalArgs[i];
    if (arg.StartsWith("urls=", StringComparison.OrdinalIgnoreCase))
    {
        urlsOverride = arg["urls=".Length..];
    }
    else if (arg.StartsWith("connstring=", StringComparison.OrdinalIgnoreCase))
    {
        connStringOverride = arg["connstring=".Length..].Trim('"');
    }
    else
    {
        // Сохраняем остальные аргументы без изменений
        processedArgs.Add(arg);
    }
}

// Преобразуем кастомный connstring в стандартную строку подключения
if (!string.IsNullOrWhiteSpace(connStringOverride))
{
    // Формат: "host@PgSQL;dbname"
    if (connStringOverride.Contains("@") && connStringOverride.Contains(";"))
    {
        var parts = connStringOverride.Split('@', 2);
        var host = parts[0];
        var rest = parts[1];
        var dbParts = rest.Split(';', 2);
        var dbName = dbParts.Length > 1 ? dbParts[1] : "logmonitor";

        // Стандартная строка подключения для PostgreSQL
        var standardConn = $"Host={host};Port=5432;Database={dbName};Username=postgres;Password=postgres";
        processedArgs.Add("--ConnectionStrings:DefaultConnection");
        processedArgs.Add(standardConn);
    }
}

// Добавляем urls как --urls
if (!string.IsNullOrWhiteSpace(urlsOverride))
{
    processedArgs.Add("--urls");
    processedArgs.Add(urlsOverride);
}

// Обновляем args для WebApplicationBuilder
args = processedArgs.ToArray();
// =========================================

// 1. Определяем путь к логам
var logDir = Path.Combine(Directory.GetCurrentDirectory(), "log");
Directory.CreateDirectory(logDir);

// 2. Создаём WebApplicationBuilder — ТОЛЬКО ОН даёт доступ к Configuration
var builder = WebApplication.CreateBuilder(args);

// CLI как источник конфигурации (низкий приоритет)
builder.Configuration.AddCommandLine(args);

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
{
    opt.UseNpgsql(conn);
    opt.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection"));
// Телеграмм
builder.Services.Configure<TelegramOptions>(
    builder.Configuration.GetSection("Telegram"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<TelegramService>();
builder.Services.AddHostedService<TelegramPollingService>();


builder.Services.AddSingleton<IErrorDetectionService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new ErrorDetectionService(config);
});
builder.Services.AddSingleton<LogMonitor.Core.Services.IFileMonitoringService, LogMonitor.Infrastructure.Services.HybridFileWatcher>();
builder.Services.AddSingleton<LogMonitor.Core.Services.INotificationRouter, LogMonitor.API.Services.NotificationRouter>();
builder.Services.AddHostedService<LogMonitor.Infrastructure.BackgroundServices.LogMonitoringHostedService>();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // или *
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // ← обязательно для SignalR с куками
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LogMonitor API", Version = "v1" });
});

var app = builder.Build();
app.MapHealthChecks("/health");

app.Logger.LogInformation("🔧 Приложение сконфигурировано. Запуск хоста...");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseCors("AllowFrontend");
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