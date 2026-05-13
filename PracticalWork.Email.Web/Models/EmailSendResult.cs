namespace PracticalWork.Email.Web.Models;

/// <summary>
/// Результат отправки email
/// </summary>
public class EmailSendResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    
    public static EmailSendResult Ok() => new() { Success = true };
    
    public static EmailSendResult Fail(string errorMessage) => new() 
    { 
        Success = false, 
        ErrorMessage = errorMessage 
    };
}