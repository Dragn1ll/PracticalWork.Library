namespace PracticalWork.Email.Web.Configuration;

/// <summary>
/// Настройки SMTP сервера для отправки email уведомлений
/// </summary>
public class EmailSettings
{
    public string SmtpServer { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 25;
    public bool UseSsl { get; set; } = false;
    public string SenderName { get; set; } = "Библиотека";
    public string SenderEmail { get; set; } = "noreply@library.local";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}