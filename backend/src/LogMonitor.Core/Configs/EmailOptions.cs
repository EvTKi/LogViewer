namespace LogMonitor.Core.Configs;

public class EmailOptions
{
    public bool IsEnabled { get; set; }
    public string? SmtpServer { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    public string[]? ToEmails { get; set; }
}