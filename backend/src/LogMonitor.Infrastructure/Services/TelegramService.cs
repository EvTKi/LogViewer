using System.Net.Http.Json;
using System.Text.Json;
using LogMonitor.Core.Configs;
using LogMonitor.Core.Entities;
using LogMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http;

using LogMonitor.Core.Dtos;
namespace LogMonitor.Infrastructure.Services;

public class TelegramService
{
    private readonly TelegramOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramService> _logger;
    private readonly HttpClient _httpClient;

    public TelegramService(
    IOptions<TelegramOptions> options,
    IServiceProvider serviceProvider, // ← внедряем провайдер
    ILogger<TelegramService> logger,
    IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<bool> SendErrorAsync(ErrorEntity error)
    {
        if (!_options.IsEnabled)
        {
            _logger.LogDebug("Отправка в Telegram отключена (IsEnabled=false)");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.BotToken) || string.IsNullOrWhiteSpace(_options.ChatId))
        {
            _logger.LogWarning("Telegram: не задан BotToken или ChatId");
            return false;
        }

        var message = $"🚨 Новая ошибка в логе!\n" +
                      $"Файл: {error.FileName}\n" +
                      $"Время: {error.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
                      $"Содержимое:\n{error.Content}";

        var payload = new
        {
            chat_id = _options.ChatId,
            text = message,
            parse_mode = "HTML"
        };

        var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                _logger.LogDebug("Telegram: попытка #{Attempt} отправить сообщение", attempt);
                var response = await _httpClient.PostAsJsonAsync(url, payload);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Уведомление отправлено в Telegram (ошибка ID: {ErrorId})", error.Id);
                    await MarkAsSentAsync(error.Id);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Telegram: HTTP {StatusCode} — {ErrorContent} (попытка {Attempt})", 
                        response.StatusCode, errorContent, attempt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram: ошибка при отправке (попытка {Attempt})", attempt);
            }

            if (attempt < 3)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s → 2s → 4s
                await Task.Delay(delay);
            }
        }

        _logger.LogError("❌ Все попытки отправки в Telegram исчерпаны для ошибки ID: {ErrorId}", error.Id);
        return false;
    }

    public async Task SendErrorNotificationAsync(ErrorDto errorDto)
    {
        if (!_options.IsEnabled || string.IsNullOrWhiteSpace(_options.BotToken))
            return;

        var message = $"🚨 Новая ошибка в логе!\nФайл: {errorDto.FileName}\nВремя: {errorDto.CreatedAt:yyyy-MM-dd HH:mm:ss}\nСодержимое:\n{errorDto.Content}";

        var sendTasks = new List<Task<bool>>();

        // 1. Отправка в указанный чат (если задан)
        if (!string.IsNullOrWhiteSpace(_options.ChatId))
        {
            sendTasks.Add(SendMessageToChatAsync(_options.ChatId!, message));
        }

        // 2. Отправка подписчикам
        sendTasks.Add(SendMessageToSubscribersAsync(message));

        // Ждём все отправки
        var results = await Task.WhenAll(sendTasks);

        // Если хотя бы одна отправка успешна — обновляем флаг
        if (results.Any(success => success))
        {
            await MarkAsSentAsync(errorDto.Id);
        }
    }

    private async Task MarkAsSentAsync(int errorId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();

        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.ErrorId == errorId);

        if (notification != null)
        {
            notification.TelegramSent = true;
            await dbContext.SaveChangesAsync();
        }
    }
    public async Task SendToAllSubscribersAsync(string messageText)
    {
        if (!_options.IsEnabled) return;

        // Получаем DbContext через scope, как в MarkAsSentAsync
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();

        var subscribers = await dbContext.TelegramSubscribers
            .Where(s => s.ChatId > 0 && s.IsActive)
            .ToListAsync();

        foreach (var sub in subscribers)
        {
            await SendMessageToChatAsync(sub.ChatId.ToString(), messageText);
        }
    }
    private async Task<bool> SendMessageToChatAsync(string chatId, string text)
    {
        var payload = new { chat_id = chatId, text, parse_mode = "HTML" };
        var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Отправлено в Telegram чат {ChatId}", chatId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка отправки в чат {ChatId} (попытка {Attempt})", chatId, attempt);
            }
            if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));
        }
        return false;
    }

    private async Task<bool> SendMessageToSubscribersAsync(string text)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();

        var subscribers = await dbContext.TelegramSubscribers
            .Where(s => s.IsActive && s.ChatId > 0)
            .ToListAsync();

        var tasks = subscribers.Select(s => SendMessageToChatAsync(s.ChatId.ToString(), text));
        var results = await Task.WhenAll(tasks);
        return results.Any(r => r);
    }
}