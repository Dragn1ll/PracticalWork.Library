using PracticalWork.Library.Dto;
using PracticalWork.Library.Enums;

namespace PracticalWork.Library.Abstractions.Services;

/// <summary>
/// Сервис по работе библиотеки
/// </summary>
public interface ILibraryService
{
    /// <summary>
    /// Выдача книги
    /// </summary>
    /// <param name="bookId">Идентификатор книги</param>
    /// <param name="readerId">Идентификатор пользователя</param>
    /// <returns>Идентификатор выдачи книги</returns>
    Task<Guid> BorrowBook(Guid bookId, Guid readerId);
    
    /// <summary>
    /// Получение списка книг библиотеки
    /// </summary>
    /// <returns>Список книг</returns>
    Task<IList<LibraryBookDto>> GetLibraryBooks(BookCategory category, string author, bool availableOnly, int page, 
        int pageSize);
    
    /// <summary>
    /// Возврат книги
    /// </summary>
    /// <param name="bookId">Идентификатор книги</param>
    Task ReturnBook(Guid bookId);
    
    /// <summary>
    /// Получение деталей книги по идентификатору
    /// </summary>
    /// <param name="bookId">Идентификатор книги</param>
    /// <returns>Детали книги</returns>
    Task<BookDetailsDto> GetBookDetailsById(Guid bookId);
    
    /// <summary>
    /// Получение деталей книги по названию
    /// </summary>
    /// <param name="title">Название книги</param>
    /// <returns>Детали книги</returns>
    Task<BookDetailsDto> GetBookDetailsByTitle(string title);
}