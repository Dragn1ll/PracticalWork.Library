using PracticalWork.Library.Enums;

namespace PracticalWork.Library.Dto;

/// <summary>
/// Данные о книге из списка книг
/// </summary>
public sealed class BookListDto
{
    /// <summary>Идентификатор книги</summary>
    public Guid BookId { get; set; }
    
    /// <summary>Название книги</summary>
    public string Title { get; set; }

    /// <summary>Авторы</summary>
    public IReadOnlyList<string> Authors { get; set; }

    /// <summary>Краткое описание книги</summary>
    public string Description { get; set; }

    /// <summary>Год издания</summary>
    public int Year { get; set; }

    /// <summary>Категория</summary>
    public BookCategory Category { get; set; }

    /// <summary>Статус</summary>
    public BookStatus Status { get; set; }

    /// <summary>Путь к изображению обложки</summary>
    public string CoverImagePath { get; set; }

    public BookListDto(Guid bookId, string title, IReadOnlyList<string> authors, string description, int year,
        BookCategory category, BookStatus status, string coverImagePath)
    {
        BookId = bookId;
        Title = title;
        Authors = authors;
        Description = description;
        Year = year;
        Category = category;
        Status = status;
        CoverImagePath = coverImagePath;
    }
}