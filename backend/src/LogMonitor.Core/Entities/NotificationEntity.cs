namespace LogMonitor.Core.Entities;

public class NotificationEntity
{
    public int Id { get; set; }
    public int ErrorId { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    public bool EmailSent { get; set; }
    public bool TelegramSent { get; set; }

    public ErrorEntity? Error { get; set; }
}