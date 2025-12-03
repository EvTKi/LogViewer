using System.Security.Cryptography;
using System.Text;
using LogMonitor.Core.Services;

namespace LogMonitor.Infrastructure.Services;

public class ErrorDetectionService : IErrorDetectionService
{
    public bool IsErrorLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        return line.Contains("ERR", StringComparison.OrdinalIgnoreCase);
    }

    public string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant(); // 64 символа, lowercase
    }
}