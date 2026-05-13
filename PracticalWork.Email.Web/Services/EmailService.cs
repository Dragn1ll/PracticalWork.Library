using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PracticalWork.Email.Web.Abstractions;
using PracticalWork.Email.Web.Configuration;
using PracticalWork.Email.Web.Models;

namespace PracticalWork.Email.Web.Services;

/// <inheritdoc cref="IEmailService"/>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc cref="IEmailService.SendAsync"/>
    public async Task<EmailSendResult> SendAsync(EmailMessage message)
    {
        try
        {
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            mimeMessage.To.Add(MailboxAddress.Parse(message.To));
            mimeMessage.Subject = message.Subject;

            var bodyBuilder = new BodyBuilder();
            if (message.IsHtml)
            {
                bodyBuilder.HtmlBody = message.HtmlBody;
                bodyBuilder.TextBody = message.TextBody;
            }
            else
            {
                bodyBuilder.TextBody = message.TextBody;
            }

            mimeMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            var secureSocketOptions = _settings.UseSsl 
                ? SecureSocketOptions.StartTls 
                : SecureSocketOptions.None;

            await client.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, secureSocketOptions);
            
            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password);
            }

            await client.SendAsync(mimeMessage);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email успешно отправлен на {Recipient} с темой '{Subject}'", 
                message.To, message.Subject);

            return EmailSendResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки email на {Recipient} с темой '{Subject}': {Error}", 
                message.To, message.Subject, ex.Message);
            return EmailSendResult.Fail(ex.Message);
        }
    }
}