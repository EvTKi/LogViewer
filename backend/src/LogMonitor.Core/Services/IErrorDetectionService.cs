namespace LogMonitor.Core.Services;

public interface IErrorDetectionService
{
    bool IsErrorLine(string line);
    string ComputeHash(string content);
}