using System.Text.RegularExpressions;
using LogMonitor.Core.Services;

namespace LogMonitor.Infrastructure.Services;

public class ErrorDetectionService : IErrorDetectionService
{
    private readonly List<ErrorPattern> _patterns = new();

    private class ErrorPattern
    {
        public string Raw { get; set; } = string.Empty;
        public Regex? Regex { get; set; }
        public bool IsSimple { get; set; }
    }

    // 🔹 Принимаем паттерны как параметр — без IConfiguration!
    public ErrorDetectionService(string[] errorPatterns)
    {
        if (errorPatterns == null || errorPatterns.Length == 0)
        {
            errorPatterns = new[] { "ERR" };
        }

        foreach (var p in errorPatterns)
        {
            if (IsRegexPattern(p))
            {
                try
                {
                    _patterns.Add(new ErrorPattern
                    {
                        Raw = p,
                        Regex = new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase),
                        IsSimple = false
                    });
                }
                catch
                {
                    _patterns.Add(new ErrorPattern { Raw = p, IsSimple = true });
                }
            }
            else
            {
                _patterns.Add(new ErrorPattern { Raw = p, IsSimple = true });
            }
        }
    }

    public bool IsErrorLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        return _patterns.Any(p =>
            p.IsSimple
                ? line.Contains(p.Raw, StringComparison.OrdinalIgnoreCase)
                : p.Regex?.IsMatch(line) == true
        );
    }

    public string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsRegexPattern(string pattern)
    {
        return pattern.Contains('^') || pattern.Contains('$') || 
               pattern.Contains('*') || pattern.Contains('+') || 
               pattern.Contains('(') || pattern.Contains('[') ||
               pattern.Contains('\\');
    }
}