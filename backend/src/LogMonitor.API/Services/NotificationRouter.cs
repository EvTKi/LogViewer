// Путь: LogMonitor.API/Services/NotificationRouter.cs
using LogMonitor.Core.Dtos;
using LogMonitor.Core.Services;
using Microsoft.AspNetCore.SignalR;
using LogMonitor.API.Hubs;
using LogMonitor.Infrastructure.Services;
using LogMonitor.Infrastructure.Data;

namespace LogMonitor.API.Services;

public class NotificationRouter : INotificationRouter
{
    private readonly IHubContext<ErrorNotificationHub> _hubContext;
    private readonly TelegramService _telegramService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationRouter> _logger;

    public NotificationRouter(
    IHubContext<ErrorNotificationHub> hubContext,
    TelegramService telegramService,
    IServiceProvider serviceProvider, // ← провайдер
    ILogger<NotificationRouter> logger)
    {
        _hubContext = hubContext;
        _telegramService = telegramService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task RouteErrorAsync(ErrorDto errorDto)
    {
        // SignalR
        await _hubContext.Clients.All.SendAsync("ReceiveError", errorDto);

        // Telegram — всем подписчикам
        var message = $"🚨 Новая ошибка!\nФайл: {errorDto.FileName}\nВремя: {errorDto.CreatedAt:yyyy-MM-dd HH:mm:ss}\nСодержимое:\n{errorDto.Content}";
        _ = _telegramService.SendToAllSubscribersAsync(message); // fire-and-forget
    }
    
}