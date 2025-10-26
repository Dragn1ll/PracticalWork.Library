using PracticalWork.Library.Enums;

namespace PracticalWork.Library.Dto;

public sealed class BookListDto
{
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

    public BookListDto(string title, IReadOnlyList<string> authors, string description, int year,
        BookCategory category, BookStatus status, string coverImagePath)
    {
        Title = title;
        Authors = authors;
        Description = description;
        Year = year;
        Category = category;
        Status = status;
        CoverImagePath = coverImagePath;
    }
}