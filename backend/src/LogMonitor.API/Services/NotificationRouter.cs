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
    private readonly ILogger<NotificationRouter> _logger;

    public NotificationRouter(
        IHubContext<ErrorNotificationHub> hubContext,
        TelegramService telegramService,
        ILogger<NotificationRouter> logger)
    {
        _hubContext = hubContext;
        _telegramService = telegramService;
        _logger = logger;
    }

    public async Task RouteErrorAsync(ErrorDto errorDto)
    {
        // 1. SignalR
        await _hubContext.Clients.All.SendAsync("ReceiveError", errorDto);

        // 2. Telegram — оба режима (чат + подписчики)
        try
        {
            await _telegramService.SendErrorNotificationAsync(errorDto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при отправке в Telegram для ошибки {Id}", errorDto.Id);
        }
    }
}