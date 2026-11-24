using System.Net;
using System.Net.Mail;
using System.Threading.Channels;

namespace CarReliability.Services;

public record EmailMessage(string To, string Subject, string HtmlBody);

/// <summary>US11: опашка за имейли — изпращат се във фонов процес, за да не бавят сайта.</summary>
public class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken token) => _channel.Reader.ReadAllAsync(token);
}

/// <summary>Фонов процес (Background Worker), който изпраща имейлите от опашката чрез SMTP.
/// Ако в appsettings.json няма настроен SMTP сървър, имейлите само се записват в лога.</summary>
public class EmailBackgroundService : BackgroundService
{
    private readonly EmailQueue _queue;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailBackgroundService> _logger;

    public EmailBackgroundService(EmailQueue queue, IConfiguration config, ILogger<EmailBackgroundService> logger)
    {
        _queue = queue;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await SendAsync(message, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Грешка при изпращане на имейл до {To}", message.To);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // спиране на приложението
        }
    }

    private async Task SendAsync(EmailMessage message, CancellationToken token)
    {
        var host = _config["Email:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation("[Имейл не е изпратен — няма SMTP настройки] До: {To}, Тема: {Subject}", message.To, message.Subject);
            return;
        }

        using var client = new SmtpClient(host, int.TryParse(_config["Email:Port"], out var port) ? port : 587)
        {
            EnableSsl = !bool.TryParse(_config["Email:EnableSsl"], out var ssl) || ssl,
            Credentials = new NetworkCredential(_config["Email:UserName"], _config["Email:Password"])
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(_config["Email:From"] ?? "noreply@avtonadejdnost.bg", _config["Email:FromName"] ?? "АвтоНадеждност.бг"),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(message.To);

        await client.SendMailAsync(mail, token);
        _logger.LogInformation("Изпратен имейл до {To}", message.To);
    }
}
