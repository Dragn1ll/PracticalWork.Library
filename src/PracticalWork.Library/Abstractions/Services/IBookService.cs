using Microsoft.AspNetCore.Http;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Abstractions.Services;

/// <summary>
/// Сервис по работе с книгами
/// </summary>
public interface IBookService
{
    /// <summary>
    /// Создание книги
    /// </summary>
    /// <param name="book">Книга</param>
    /// <returns>Идентификатор созданной книги</returns>
    Task<Guid> CreateBook(Book book);
    
    /// <summary>
    /// Редактирование книги
    /// </summary>
    /// <param name="bookId">Идентификатор книги</param>
    /// <param name="book">Книга с изменёнными данными</param>
    Task UpdateBook(Guid bookId, Book book);
    
    /// <summary>
    /// Перевод книги в архив
    /// </summary>
    /// <param name="bookId">Идентификатор книги</param>
    Task ArchiveBook(Guid bookId);
    
    /// <summary>
    /// Получение списка книг
    /// </summary>
    /// <returns>Список книг</returns>
    Task<ICollection<Book>> GetBooks(BookStatus status, BookCategory category, string author, int page, int pageSize);

    /// <summary>
    /// Добавление деталей книги
    /// </summary>
    /// <param name="bookId">Идентификатор книги</param>
    /// <param name="coverImage">Изображение обложки книги</param>
    /// <param name="description">Краткое описание книги</param>
    Task CreateBookDetails(Guid bookId, IFormFile coverImage, string description);
}