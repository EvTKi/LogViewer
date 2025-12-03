namespace LogMonitor.Core.Dtos;

public record ConfigureRequest(
    string LogDirectory,
    string[] FileMasks,
    string[]? ToEmails = null,
    string? TelegramChatId = null
);