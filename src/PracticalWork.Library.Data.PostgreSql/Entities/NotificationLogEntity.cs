using PracticalWork.Library.Abstractions.Storage.Entity;

namespace PracticalWork.Library.Data.PostgreSql.Entities;

/// <summary>
/// Лог уведомлений об отправке email
/// </summary>
public sealed class NotificationLogEntity : EntityBase
{
    /// <summary>Идентификатор связанной выдачи книги</summary>
    public Guid? BorrowId { get; set; }
    
    /// <summary>Тип уведомления (ReturnReminder, WeeklyReport)</summary>
    public string NotificationType { get; set; } = null!;
    
    /// <summary>Email получателя</summary>
    public string RecipientEmail { get; set; } = null!;
    
    /// <summary>Тема письма</summary>
    public string Subject { get; set; } = null!;
    
    /// <summary>Статус отправки</summary>
    public string Status { get; set; } = null!;
    
    /// <summary>Сообщение об ошибке</summary>
    public string ErrorMessage { get; set; }
}