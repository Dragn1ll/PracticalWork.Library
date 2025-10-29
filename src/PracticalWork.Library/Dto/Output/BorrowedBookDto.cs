namespace PracticalWork.Library.Dto.Output;

/// <summary>
/// Данные о выданной книге
/// </summary>
public sealed class BorrowedBookDto
{
    /// <summary>Идентификатор книги</summary>
    public Guid BookId { get; set; }
    
    /// <summary>дата выдачи</summary>
    public DateOnly BorrowDate { get; set; }
    
    /// <summary>Срок возврата книги</summary>
    public DateOnly DueDate { get; set; }

    public BorrowedBookDto(Guid bookId,DateOnly borrowDate,  DateOnly dueDate)
    {
        BookId = bookId;
        BorrowDate = borrowDate;
        DueDate = dueDate;
    }
}