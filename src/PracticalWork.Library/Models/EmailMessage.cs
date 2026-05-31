namespace PracticalWork.Library.Models;

/// <summary>
/// Email сообщение для отправки
/// </summary>
public sealed class EmailMessage
{
    public string RecipientName { get; set; }
    public string EmailTo { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public bool IsHtml { get; set; } = true;
    public string BodyEncoding { get; set; } = "UTF-8";
    public string SubjectEncoding { get; set; } = "UTF-8";
}