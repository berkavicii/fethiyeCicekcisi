using KedWear.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace KedWear.Application.Services;

/// <summary>Sends real email via SMTP (MailKit). Registered only when Smtp:Host is configured
/// — see DependencyInjection.AddApplication, which falls back to NullEmailService otherwise.</summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var smtp = _configuration.GetSection("Smtp");
        var fromEmail = smtp["FromEmail"] ?? "info@kedwear.com";
        var fromName = smtp["FromName"] ?? "KedWear";
        var host = smtp["Host"] ?? string.Empty;
        var port = int.TryParse(smtp["Port"], out var p) ? p : 587;
        var username = smtp["Username"] ?? string.Empty;
        var password = smtp["Password"] ?? string.Empty;
        var useSsl = !bool.TryParse(smtp["UseSsl"], out var ssl) || ssl;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

        try
        {
            using var client = new MailKit.Net.Smtp.SmtpClient();
            await client.ConnectAsync(host, port, useSsl
                ? MailKit.Security.SecureSocketOptions.StartTls
                : MailKit.Security.SecureSocketOptions.None);
            if (!string.IsNullOrEmpty(username))
                await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            _logger.LogInformation("E-posta gönderildi: {To}, konu: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // Email failures must never break the caller's main flow (registration, order
            // confirmation) — log it so it's visible in ops, but don't throw.
            _logger.LogError(ex, "E-posta gönderilemedi: {To}, konu: {Subject}", toEmail, subject);
        }
    }
}

/// <summary>Used when no SMTP provider is configured yet — logs what would have been sent
/// instead of failing, so the rest of the app (registration, checkout) keeps working while
/// a real provider (Brevo, Gmail, domain mail) is set up.</summary>
public class NullEmailService : IEmailService
{
    private readonly ILogger<NullEmailService> _logger;

    public NullEmailService(ILogger<NullEmailService> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        // Pull out any links (confirm/reset URLs) so they're still usable for manual testing
        // before a real provider is wired up — without dumping the whole HTML into the log.
        var links = System.Text.RegularExpressions.Regex.Matches(htmlBody, "href=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value);

        _logger.LogWarning(
            "SMTP yapılandırılmamış, e-posta GÖNDERİLMEDİ (sadece loglandı). Alıcı: {To} <{Email}>, Konu: {Subject}, Bağlantı(lar): {Links}",
            toName, toEmail, subject, string.Join(" | ", links));
        return Task.CompletedTask;
    }
}
