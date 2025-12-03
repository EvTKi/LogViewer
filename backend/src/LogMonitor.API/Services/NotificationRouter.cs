// Путь: LogMonitor.API/Services/NotificationRouter.cs
using LogMonitor.Core.Dtos;
using LogMonitor.Core.Services;
using Microsoft.AspNetCore.SignalR;
using LogMonitor.API.Hubs;

namespace LogMonitor.API.Services;

public class NotificationRouter : INotificationRouter
{
    private readonly IHubContext<ErrorNotificationHub> _hubContext;

    public NotificationRouter(IHubContext<ErrorNotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task RouteErrorAsync(ErrorDto errorDto)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveError", errorDto);
    }
}