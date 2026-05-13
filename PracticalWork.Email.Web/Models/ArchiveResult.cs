namespace PracticalWork.Email.Web.Models;

/// <summary>
/// Результат выполнения архивации книг
/// </summary>
public class ArchiveResult
{
    public int TotalProcessed { get; set; }
    public int ArchivedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public List<ArchiveSkippedBook> SkippedBooks { get; set; } = new();
    public List<ArchiveErrorBook> ErrorBooks { get; set; } = new();
}

public class ArchiveSkippedBook
{
    public Guid BookId { get; set; }
    public string Title { get; set; } = null!;
    public string Reason { get; set; } = null!;
}

public class ArchiveErrorBook
{
    public Guid BookId { get; set; }
    public string Title { get; set; } = null!;
    public string Error { get; set; } = null!;
}