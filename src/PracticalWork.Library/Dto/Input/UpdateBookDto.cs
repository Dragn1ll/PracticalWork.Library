namespace PracticalWork.Library.Dto.Input;

/// <summary>
/// Данные для изменения книги
/// </summary>
public sealed class UpdateBookDto
{
    /// <summary>Название книги</summary>
    public string Title { get; }
    
    /// <summary>Авторы</summary>
    public IReadOnlyList<string> Authors { get; }

    /// <summary>Краткое описание книги</summary>
    public string Description { get; }

    /// <summary>Год издания</summary>
    public int Year { get; }

    public UpdateBookDto(string title, IReadOnlyList<string> authors, string description, int year)
    {
        Title = title;
        Authors = authors;
        Description = description;
        Year = year;
    }
}