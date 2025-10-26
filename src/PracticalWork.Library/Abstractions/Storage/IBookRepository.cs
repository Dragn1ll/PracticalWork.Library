using PracticalWork.Library.Enums;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Abstractions.Storage;

/// <summary>
/// Репозиторий для Книг
/// </summary>
public interface IBookRepository
{
    /// <summary>
    /// Создать книгу
    /// </summary>
    /// <param name="book">Книга</param>
    /// <returns>Идентификатор созданной книги</returns>
    Task<Guid> CreateBook(Book book);

    /// <summary>
    /// Получить книгу
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns>Книга</returns>
    Task<Book> GetBook(Guid bookId);
    
    /// <summary>
    /// Обновить данные книги
    /// </summary>
    /// <param name="bookId">Идентификатор книги</param>
    /// <param name="book">Книга</param>
    Task UpdateBook(Guid bookId, Book book);

    /// <summary>
    /// Получить список книг
    /// </summary>
    /// <param name="status">Статус книги</param>
    /// <param name="category">Категория книги</param>
    /// <param name="author">Автор книги</param>
    /// <param name="page">Номер страницы</param>
    /// <param name="pageSize">Размер страниц</param>
    /// <returns>Список книг</returns>
    Task<ICollection<Book>> GetBooks(BookStatus status, BookCategory category, string author, int page, int pageSize);
}