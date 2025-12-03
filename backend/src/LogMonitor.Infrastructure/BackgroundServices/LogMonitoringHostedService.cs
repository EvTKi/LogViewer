using LogMonitor.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace LogMonitor.Infrastructure.BackgroundServices;

public class LogMonitoringHostedService : IHostedService
{
    private readonly IFileMonitoringService _monitoringService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogMonitoringHostedService> _logger;

    public LogMonitoringHostedService(
        IFileMonitoringService monitoringService,
        IConfiguration configuration,
        ILogger<LogMonitoringHostedService> logger) // ← внедри логгер
    {
        _monitoringService = monitoringService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("🔄 Фоновая служба мониторинга логов запускается...");
        var logDir = _configuration["Monitoring:LogDirectory"] ?? @"D:\logs";
        var masks = (_configuration["Monitoring:FileMasks"] ?? "*.log")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        _logger.LogInformation("📁 Отслеживаемая директория: {LogDir}, маски: {Masks}", logDir, string.Join(", ", masks));

        await _monitoringService.StartMonitoringAsync(logDir, masks);
        _logger.LogInformation("✅ Фоновая служба мониторинга логов запущена.");
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("⏹️ Фоновая служба мониторинга логов останавливается...");
        await _monitoringService.StopMonitoringAsync();
        _logger.LogInformation("⏹️ Фоновая служба остановлена.");
    }
}