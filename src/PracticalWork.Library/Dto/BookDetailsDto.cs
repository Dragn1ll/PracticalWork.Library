namespace PracticalWork.Library.Dto;

/// <summary>
/// Детали книги(описание, путь к изображению обложки)
/// </summary>
public class BookDetailsDto
{
    /// <summary>Краткое описание книги</summary>
    public string Description { get; set; }
    
    /// <summary>Путь к изображению обложки</summary>
    public string CoverImagePath { get; set; }

    public BookDetailsDto(string description, string coverImagePath)
    {
        Description = description;
        CoverImagePath = coverImagePath;
    }
}