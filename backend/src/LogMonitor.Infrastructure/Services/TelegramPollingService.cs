using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using LogMonitor.Core.Configs;
using LogMonitor.Core.Entities;
using LogMonitor.Infrastructure.Data;

namespace LogMonitor.Infrastructure.Services;


// TODO: добавить распознавание кнопки unsubscrib
public class TelegramPollingService : BackgroundService
{
    private readonly TelegramOptions _options;
    private readonly IServiceProvider _serviceProvider; // ← правильно
    private readonly ILogger<TelegramPollingService> _logger;
    private readonly HttpClient _httpClient;
    private long _lastUpdateId = 0;

    public TelegramPollingService(
        IOptions<TelegramOptions> options,
        IServiceProvider serviceProvider,
        ILogger<TelegramPollingService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsEnabled) return;

        _logger.LogInformation("Telegram polling запущен");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollUpdates(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); // не чаще 2 сек
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в Telegram polling");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task PollUpdates(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();

        var url = $"https://api.telegram.org/bot{_options.BotToken}/getUpdates?offset={_lastUpdateId + 1}&timeout=30";
        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var updates = doc.RootElement.GetProperty("result");

        foreach (var update in updates.EnumerateArray())
        {
            var updateId = update.GetProperty("update_id").GetInt64();
            _lastUpdateId = Math.Max(_lastUpdateId, updateId);

            if (!update.TryGetProperty("message", out var message)) continue;
            if (!message.TryGetProperty("text", out var textElem)) continue;

            var text = textElem.GetString();
            if (text != "/start") continue;

            var chat = message.GetProperty("chat");
            var chatId = chat.GetProperty("id").GetInt64();

            // Только личные чаты (chatId > 0)
            if (chatId <= 0) continue;

            var firstName = chat.TryGetProperty("first_name", out var fn) ? fn.GetString() : null;
            var username = chat.TryGetProperty("username", out var un) ? un.GetString() : null;

            var existing = await dbContext.TelegramSubscribers.FindAsync(chatId);
            if (existing == null)
            {
                dbContext.TelegramSubscribers.Add(new TelegramSubscriberEntity
                {
                    ChatId = chatId,
                    FirstName = firstName,
                    Username = username
                });
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("✅ Новый подписчик Telegram: {ChatId} (@{Username})", chatId, username);
            }
        }
    }

}