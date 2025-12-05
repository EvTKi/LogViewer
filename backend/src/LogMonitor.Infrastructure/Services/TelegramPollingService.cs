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

public class TelegramPollingService : BackgroundService
{
    private readonly TelegramOptions _options;
    private readonly IServiceProvider _serviceProvider;
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
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в Telegram polling");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        _logger.LogInformation("Telegram polling остановлен");
    }

    private async Task PollUpdates(CancellationToken ct)
    {
        if (!_options.IsEnabled || string.IsNullOrWhiteSpace(_options.BotToken))
            return;

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();

        var url = $"https://api.telegram.org/bot{_options.BotToken}/getUpdates?offset={_lastUpdateId + 1}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, ct);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            _logger.LogWarning(ex, "Не удаётся подключиться к Telegram API");
            return;
        }

        try
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Telegram getUpdates вернул статус {StatusCode}: {ErrorContent}", 
                    response.StatusCode, errorContent);
                return;
            }

            string json;
            try
            {
                json = await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при чтении тела ответа от Telegram");
                return;
            }

            // 🔍 Проверка: получен именно JSON, а не HTML (например, NTA-блокировка)
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
            {
                _logger.LogWarning("Получен не-JSON от Telegram (возможно, блокировка). Первые 200 символов:\n{Preview}", 
                    json.Length > 200 ? json[..200] : json);
                return;
            }

            _logger.LogDebug("Получен ответ от Telegram: {Json}", json);

            // 🔸 Парсим JSON
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("ok", out var okElem) || !okElem.GetBoolean())
            {
                _logger.LogWarning("Telegram API вернул ошибку: {Json}", json);
                return;
            }

            if (!doc.RootElement.TryGetProperty("result", out var updates) || 
                updates.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Отсутствует или неверный 'result' в ответе Telegram");
                return;
            }

            foreach (var update in updates.EnumerateArray())
            {
                if (!update.TryGetProperty("update_id", out var updateIdElem) ||
                    !long.TryParse(updateIdElem.ToString(), out var updateId))
                {
                    continue;
                }

                // Обновляем offset ДО обработки (идемпотентность обеспечена логикой)
                _lastUpdateId = Math.Max(_lastUpdateId, updateId);

                if (!update.TryGetProperty("message", out var message))
                    continue;

                if (!message.TryGetProperty("text", out var textElem) || 
                    string.IsNullOrWhiteSpace(textElem.GetString()))
                    continue;

                var fullText = textElem.GetString()!;
                _logger.LogDebug("Получена команда: '{Command}' (update_id={UpdateId})", fullText, updateId);

                // 🔹 Извлекаем чистую команду: "/start" или "/start@MyBot"
                string command = fullText.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                        .FirstOrDefault() ?? "";

                // Только личные чаты
                if (!message.TryGetProperty("chat", out var chat))
                    continue;

                if (!chat.TryGetProperty("id", out var chatIdElem) ||
                    !long.TryParse(chatIdElem.ToString(), out var chatId) ||
                    chatId <= 0)
                    continue;

                var firstName = chat.TryGetProperty("first_name", out var fn) ? fn.GetString() : null;
                var username = chat.TryGetProperty("username", out var un) ? un.GetString() : null;

                bool isStart = command == "/start" || command.StartsWith("/start@");
                bool isUnsubscribe = command == "/unsubscribe" || command.StartsWith("/unsubscribe@");

                if (!isStart && !isUnsubscribe)
                {
                    _logger.LogDebug("Игнорируем неизвестную команду: {Command}", command);
                    continue;
                }

                var subscriber = await dbContext.TelegramSubscribers.FindAsync(chatId);

                if (isStart)
                {
                    if (subscriber == null)
                    {
                        subscriber = new TelegramSubscriberEntity
                        {
                            ChatId = chatId,
                            FirstName = firstName,
                            Username = username,
                            IsActive = true
                        };
                        dbContext.TelegramSubscribers.Add(subscriber);
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("✅ Новый подписчик Telegram: {ChatId} (@{Username})", chatId, username);
                    }
                    else if (!subscriber.IsActive)
                    {
                        subscriber.IsActive = true;
                        subscriber.SubscribedAt = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("✅ Пользователь {ChatId} возобновил подписку", chatId);
                    }
                    else
                    {
                        _logger.LogDebug("Пользователь {ChatId} уже подписан", chatId);
                    }
                }
                else if (isUnsubscribe)
                {
                    if (subscriber != null && subscriber.IsActive)
                    {
                        subscriber.IsActive = false;
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("🔕 Пользователь {ChatId} отписался от уведомлений", chatId);
                    }
                    else
                    {
                        _logger.LogDebug("Пользователь {ChatId} не подписан", chatId);
                    }
                }
            }
        }
        finally
        {
            response.Dispose();
        }
    }

}