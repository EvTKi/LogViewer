using LogMonitor.Core.Configs;
using LogMonitor.Core.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogMonitor.Infrastructure.Services;

public class EmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendErrorNotificationAsync(ErrorDto errorDto)
    {
        if (!_options.IsEnabled || _options.ToEmails == null || !_options.ToEmails.Any())
            return;

        try
        {
            var message = $@"
                <h2>🚨 Новая ошибка в логе!</h2>
                <p><strong>Файл:</strong> {errorDto.FileName}</p>
                <p><strong>Время:</strong> {errorDto.CreatedAt:yyyy-MM-dd HH:mm:ss}</p>
                <pre>{errorDto.Content}</pre>
            ";

            using var client = new MailKit.Net.Smtp.SmtpClient();
            await client.ConnectAsync(_options.SmtpServer!, _options.Port, MailKit.Security.SecureSocketOption.StartTls);
            await client.AuthenticateAsync(_options.Username!, _options.Password!);

            var mail = new MimeKit.MimeMessage();
            mail.From.Add(MimeKit.MailboxAddress.Parse(_options.From!));
            foreach (var to in _options.ToEmails)
                mail.To.Add(MimeKit.MailboxAddress.Parse(to));

            mail.Subject = "LogMonitor: Новая ошибка";
            mail.Body = new MimeKit.TextPart(MimeKit.Text.TextFormat.Html) { Text = message };

            await client.SendAsync(mail);
            await client.DisconnectAsync(true);

            _logger.LogInformation("✅ Email отправлен на {Recipients}", string.Join(", ", _options.ToEmails));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка отправки email");
        }
    }
}