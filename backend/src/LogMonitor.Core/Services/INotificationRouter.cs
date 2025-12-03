using LogMonitor.Core.Dtos;

namespace LogMonitor.Core.Services;

public interface INotificationRouter
{
    Task RouteErrorAsync(ErrorDto errorDto);
}