namespace LogMonitor.Core.Dtos;

public record ErrorDto(
    int Id,
    string FileName,
    string Content,
    long LinePosition,
    DateTime CreatedAt
);