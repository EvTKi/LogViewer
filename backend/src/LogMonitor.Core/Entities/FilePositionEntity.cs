namespace LogMonitor.Core.Entities;

public class FilePositionEntity
{
    public string FilePath { get; set; } = string.Empty;
    public long LastPosition { get; set; }
}