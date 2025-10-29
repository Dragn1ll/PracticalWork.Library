using PracticalWork.Library.Enums;

namespace PracticalWork.Library.Dto;

/// <summary>
/// Книга из библиотеки
/// </summary>
public class LibraryBookDto
{
    /// <summary>Название книги</summary>
    public string Title { get; set; }

    /// <summary>Авторы</summary>
    public IReadOnlyList<string> Authors { get; set; }

    /// <summary>Краткое описание книги</summary>
    public string Description { get; set; }
    
    /// <summary>Год издания</summary>
    public int Year { get; set; }

    public LibraryBookDto(string title, IReadOnlyList<string> authors, string description, int year)
    {
        Title = title;
        Authors = authors;
        Description = description;
        Year = year;
    }
}