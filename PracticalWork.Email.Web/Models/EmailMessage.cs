namespace PracticalWork.Email.Web.Models;

/// <summary>
/// Email сообщение для отправки
/// </summary>
public class EmailMessage
{
    public string To { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string HtmlBody { get; set; } = null!;
    public string TextBody { get; set; } = null!;
    public bool IsHtml { get; set; } = true;
}