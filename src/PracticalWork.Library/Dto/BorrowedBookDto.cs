namespace PracticalWork.Library.Dto;

public sealed class BorrowedBookDto
{
    /// <summary>Идентификатор возврата книги</summary>
    public Guid BorrowId { get; set; }
    
    /// <summary>Идентификатор книги</summary>
    public Guid BookId { get; set; }
    
    /// <summary>Срок возврата книги</summary>
    public DateOnly DueDate { get; set; }

    public BorrowedBookDto(Guid borrowId, Guid bookId, DateOnly dueDate)
    {
        BorrowId = borrowId;
        BookId = bookId;
        DueDate = dueDate;
    }
}