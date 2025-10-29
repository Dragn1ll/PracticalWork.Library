namespace PracticalWork.Library.Dto;

public class ArchiveBookDto
{
    /// <summary>Идентификатор книги</summary>
    public Guid Id { get; set; }
    
    /// <summary>Название книги</summary>
    public string Title { get; set; }

    /// <summary>Дата перевода в архив</summary>
    public DateTime ArchivedAt { get; set; }

    public ArchiveBookDto(Guid id, string title)
    {
        Id = id;
        Title = title;
        ArchivedAt = DateTime.Now;
    }
}