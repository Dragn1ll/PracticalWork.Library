using System.Linq.Expressions;
using PracticalWork.Library.Entities;
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
    /// <param name="updateAction">Изменения данных книги</param>
    Task UpdateBook(Guid bookId, Action<AbstractBookEntity> updateAction);
    
    /// <summary>
    /// Получить список книг
    /// </summary>
    /// <param name="page">Номер страницы</param>
    /// <param name="pageSize">Размер страниц</param>
    /// <param name="predicate">Условия, по которому отбираются книги</param>
    /// <returns>Список книг</returns>
    Task<ICollection<Book>> GetBooks(int page, int pageSize, Expression<Func<AbstractBookEntity, bool>> predicate);
}