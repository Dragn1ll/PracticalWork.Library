using PracticalWork.Library.Enums;

namespace PracticalWork.Library.Dto;

/// <summary>
/// Детальная информация о книге
/// </summary>
public class BookDetailsDto
{
    /// <summary>Идентификатор книги</summary>
    public Guid Id { get; set; }
    
    /// <summary>Название книги</summary>
    public string Title { get; set; }

    /// <summary>Категория</summary>
    public BookCategory Category { get; set; }

    /// <summary>Авторы</summary>
    public IReadOnlyList<string> Authors { get; set; }

    /// <summary>Краткое описание книги</summary>
    public string Description { get; set; }

    /// <summary>Год издания</summary>
    public int Year { get; set; }

    /// <summary>Путь к изображению обложки</summary>
    public string CoverImagePath { get; set; }

    /// <summary>Статус</summary>
    public BookStatus Status { get; set; }

    /// <summary>В архиве</summary>
    public bool IsArchived { get; set; }

    public BookDetailsDto(Guid id, string title, IReadOnlyList<string> authors, string description, int year, 
        BookCategory category, BookStatus status, string coverImagePath, bool isArchived)
    {
        Id = id;
        Title = title;
        Authors = authors;
        Description = description;
        Year = year;
        Category = category;
        Status = status;
        CoverImagePath = coverImagePath;
        IsArchived = isArchived;
    }
}