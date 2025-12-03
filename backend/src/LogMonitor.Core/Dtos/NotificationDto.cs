namespace LogMonitor.Core.Dtos;

public record NotificationDto(
    int Id,
    int ErrorId,
    DateTime SentAt,
    bool IsRead,
    bool EmailSent,
    bool TelegramSent
);