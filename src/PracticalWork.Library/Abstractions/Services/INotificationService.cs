using PracticalWork.Library.Models;

namespace PracticalWork.Library.Abstractions.Services;

public interface INotificationService
{
    Task<ReturnReminderResult> NotifyReadersReturnBooks();
}