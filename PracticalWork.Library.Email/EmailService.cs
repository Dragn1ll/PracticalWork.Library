using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Email;

/// <inheritdoc cref="IEmailService"/>
public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ISmtpClient _client;

    public EmailService(ISmtpClient client, IOptions<EmailOptions> settings)
    {
        _options = settings.Value;
        _client = client;
    }

    /// <inheritdoc cref="IEmailService.SendAsync"/>
    public async Task<EmailSendResult> SendAsync(EmailMessage message)
    {
        EmailSendResult result = new();
        try
        {
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(_options.SmtpServer, _options.SmtpPort, _options.UseSsl);
                _client.AuthenticationMechanisms.Remove("XOAUTH2");

                if (!string.IsNullOrEmpty(_options.Username))
                {
                    await _client.AuthenticateAsync(_options.Username, _options.Password);
                }
            }

            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress(_options.SenderName, _options.SenderEmail));
            mimeMessage.To.Add(new MailboxAddress(message.RecipientName, message.EmailTo));
            mimeMessage.Subject = message.Subject;
            mimeMessage.Body = GetBodyBuilder(message).ToMessageBody();

            var response = await _client.SendAsync(mimeMessage);
            result.IsSuccess = true;
            result.Message = response;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Message = ex.Message;
        }

        return result;
    }

    private static BodyBuilder GetBodyBuilder(EmailMessage message)
    {
        var builder = new BodyBuilder();
        if (message.IsHtml)
        {
            builder.HtmlBody = message.Body;
        }
        else
        {
            builder.TextBody = message.Body;
        }
        return builder;
    }
}