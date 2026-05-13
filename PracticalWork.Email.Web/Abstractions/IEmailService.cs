using PracticalWork.Email.Web.Models;

namespace PracticalWork.Email.Web.Abstractions;

/// <summary>
/// Сервис для отправки email сообщений
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Отправить email
    /// </summary>
    Task<EmailSendResult> SendAsync(EmailMessage message);
}