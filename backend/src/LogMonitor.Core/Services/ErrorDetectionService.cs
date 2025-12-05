using System.Security.Cryptography;
using System.Text;
using LogMonitor.Core.Services;
using Microsoft.Extensions.Configuration;

namespace LogMonitor.Infrastructure.Services;

public class ErrorDetectionService : IErrorDetectionService
{
    private readonly string[] _errorPatterns;

    public ErrorDetectionService(IConfiguration configuration)
    {
        var patterns = configuration["Monitoring:ErrorPatterns"];
        _errorPatterns = string.IsNullOrWhiteSpace(patterns)
            ? new[] { "ERR" }
            : patterns.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    public bool IsErrorLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        return _errorPatterns.Any(pattern =>
            line.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    public string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}