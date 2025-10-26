using PracticalWork.Library.Enums;

namespace PracticalWork.Library.Models;

/// <summary>
/// Выдача книги
/// </summary>
public sealed class Borrow
{
    /// <summary>Внешний ключ на Book</summary>
    public Guid BookId { get; set; }
    
    /// <summary>Внешний ключ на Reader</summary>
    public Guid ReaderId { get; set; }
    
    /// <summary>дата выдачи</summary>
    public DateOnly BorrowDate { get; set; }
    
    /// <summary>Срок возврата</summary>
    public DateOnly DueDate { get; set; }
    
    /// <summary>Фактическая дата возврата</summary>
    public DateOnly ReturnDate { get; set; }
    
    /// <summary>Статус</summary>
    public BookIssueStatus Status { get; set; }

    public void ReturnBook()
    {
        if (Status != BookIssueStatus.Issued)
        {
            throw new InvalidOperationException("Книга уже возвращена в библиотеку!");
        }

        ReturnDate = DateOnly.FromDateTime(DateTime.Now);
        Status = ReturnDate < DueDate 
            ? BookIssueStatus.Returned 
            : BookIssueStatus.Overdue;
    }
}