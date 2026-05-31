namespace PracticalWork.Library.Models;

/// <summary>
/// Результат выполнения архивации книг
/// </summary>
public class ArchiveResult
{
    public int TotalProcessed { get; set; }
    
    public int ArchivedCount { get; set; }
    
    public int SkippedCount { get; set; }
    
    public string SkipReasons { get; set; }
    
    public TimeSpan ExecutionTime { get; set; }
}