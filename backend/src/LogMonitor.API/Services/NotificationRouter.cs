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
        await _hubContext.Clients.All.SendAsync("ReceiveError", errorDto);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();
            var error = await dbContext.Errors.FindAsync(errorDto.Id);
            if (error != null)
            {
                _ = _telegramService.SendErrorAsync(error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при отправке в Telegram для ошибки {Id}", errorDto.Id);
        }
    }
}