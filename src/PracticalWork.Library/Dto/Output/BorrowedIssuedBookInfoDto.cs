namespace PracticalWork.Library.Dto.Output;

public class BorrowedIssuedBookInfoDto
{
    public string BookTitle { get; set; }
    
    public IReadOnlyList<string> BookAuthors { get; set; }
    
    public Guid ReaderId { get; set; }
    
    public string ReaderFullName { get; set; }
    
    public string ReaderEmail { get; set; }
    
    public Guid BorrowId { get; set; }
    
    public DateOnly BorrowDueDate { get; set; }
}