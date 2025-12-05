namespace LogMonitor.Core.Configs;

public class TelegramOptions
{
    public bool IsEnabled { get; set; }
    public string? BotToken { get; set; }
     public string? ChatId { get; set; }
}