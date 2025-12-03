namespace LogMonitor.Core.Services;

public interface IFileMonitoringService
{
    Task StartMonitoringAsync(string directory, string[] fileMasks);
    Task StopMonitoringAsync();
}