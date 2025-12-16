using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Abstractions.Storage.Repositories;

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
    /// Получить книгу по идентификатору
    /// </summary>
    /// <param name="bookId">Идентификатор книги</param>
    /// <returns>Книга</returns>
    Task<Book> GetBookById(Guid bookId);

    /// <summary>
    /// Получить список книг
    /// </summary>
    /// <param name="status">Статус книги</param>
    /// <param name="category">Категория книги</param>
    /// <param name="author">Автор книги</param>
    /// <param name="page">Номер страницы</param>
    /// <param name="pageSize">Размер страниц</param>
    /// <returns>Список книг</returns>
    Task<IList<BookListDto>> GetBooks(BookStatus status, BookCategory category, string author, int page, int pageSize);
    
    /// <summary>
    /// Обновить данные книги
    /// </summary>
    /// <param name="bookId">Идентификатор книги</param>
    /// <param name="book">Книга</param>
    Task UpdateBook(Guid bookId, Book book);
}