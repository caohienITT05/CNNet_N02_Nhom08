using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using TiemChe.Application.Interfaces;

namespace TiemChe.Infrastructure.Services;

public sealed class SmtpSettings
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? From { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool EnableSsl { get; init; } = true;
}

public sealed class SmtpResetEmailSender(SmtpSettings settings, ILogger<SmtpResetEmailSender> logger)
    : IResetEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.Host) && !string.IsNullOrWhiteSpace(settings.From);

    public async Task SendAsync(string email, string code, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SmtpClient(settings.Host!, settings.Port) { EnableSsl = settings.EnableSsl };
            if (!string.IsNullOrEmpty(settings.Username))
                client.Credentials = new NetworkCredential(settings.Username, settings.Password);
            using var message = new MailMessage(settings.From!, email, "Tiệm Chè — Đặt lại mật khẩu",
                $"Mã đặt lại mật khẩu của bạn là {code}. Mã có hiệu lực trong 15 phút và dùng một lần.");
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is SmtpException or ArgumentException or FormatException or InvalidOperationException)
        {
            // Phản hồi của API giống nhau để tránh tiết lộ email đã đăng ký.
            logger.LogWarning("Không gửi được email đặt lại mật khẩu ({ErrorType}).", ex.GetType().Name);
        }
    }
}
