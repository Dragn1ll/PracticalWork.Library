namespace PracticalWork.Library.Models;

/// <summary>
/// Результат отправки email
/// </summary>
public sealed class EmailSendResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
}