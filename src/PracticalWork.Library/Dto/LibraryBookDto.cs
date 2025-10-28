using PracticalWork.Library.Enums;

namespace PracticalWork.Library.Dto;

/// <summary>
/// Книга из библиотеки
/// </summary>
public class LibraryBookDto
{
    /// <summary>Идентификатор книги</summary>
    public Guid BookId { get; set; }
    
    /// <summary>Название книги</summary>
    public string Title { get; set; }

    /// <summary>Авторы</summary>
    public IReadOnlyList<string> Authors { get; set; }
    
    /// <summary>Год издания</summary>
    public int Year { get; set; }

    /// <summary>Категория</summary>
    public BookCategory Category { get; set; }

    /// <summary>Статус</summary>
    public BookStatus Status { get; set; }

    public LibraryBookDto(Guid bookId, string title, IReadOnlyList<string> authors, int year, 
        BookCategory category, BookStatus status)
    {
        BookId = bookId;
        Title = title;
        Authors = authors;
        Year = year;
        Category = category;
        Status = status;
    }
}