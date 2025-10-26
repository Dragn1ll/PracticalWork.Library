namespace PracticalWork.Library.Dto;

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