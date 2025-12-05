namespace LogMonitor.Core.Entities;

public class TelegramSubscriberEntity
{
    public long ChatId { get; set; }
    public string? FirstName { get; set; }
    public string? Username { get; set; }
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}