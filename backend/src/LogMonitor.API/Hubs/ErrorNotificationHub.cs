using Microsoft.AspNetCore.SignalR;
using LogMonitor.Core.Dtos;

namespace LogMonitor.API.Hubs;

public class ErrorNotificationHub : Hub
{
    public async Task Subscribe(string clientId)
    {
        // Подписываем клиента на группу (опционально)
        await Groups.AddToGroupAsync(Context.ConnectionId, clientId);
        await Clients.Caller.SendAsync("Subscribed", $"Подписан под ID: {clientId}");
    }

    // Этот метод будет вызывать инфраструктура, когда найдёт новую ошибку
    public async Task SendError(ErrorDto error)
    {
        await Clients.All.SendAsync("ReceiveError", error);
    }
}