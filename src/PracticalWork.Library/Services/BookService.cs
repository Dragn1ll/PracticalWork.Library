using Microsoft.AspNetCore.Http;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Services;

/// <inheritdoc cref="IBookService"/>
public sealed class BookService : IBookService
{
    private readonly IBookRepository _bookRepository;
    private readonly IRedisService _redisService;

    public BookService(IBookRepository bookRepository, IRedisService redisService)
    {
        _bookRepository = bookRepository;
        _redisService = redisService;
    }

    /// <inheritdoc cref="IBookService.CreateBook"/>
    public async Task<Guid> CreateBook(Book book)
    {
        book.Status = BookStatus.Available;
        try
        {
            return await _bookRepository.CreateBook(book);
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка создания книги!", ex);
        }
    }

    /// <inheritdoc cref="IBookService.UpdateBook"/>
    public async Task UpdateBook(Guid bookId, Book book)
    {
        try
        {
            var bookEntity = await _bookRepository.GetBookById(bookId);

            if (bookEntity.IsArchived)
            {
                throw new ArgumentException("Книга находится в архиве.");
            }
            
            // todo инвалидацию кэша связанных данных в Redis 
            
            await _bookRepository.UpdateBook(bookId, book);
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка обновления данных книги!", ex);
        }
    }

    /// <inheritdoc cref="IBookService.ArchiveBook"/>
    public async Task ArchiveBook(Guid bookId)
    {
        try
        {
            var book = await _bookRepository.GetBookById(bookId);

            if (book.IsArchived)
            {
                throw new ArgumentException("Книга уже переведена в архив.");
            }
            
            // todo инвалидацию кэша списков книг в Redis 
            
            book.Archive();
            
            await _bookRepository.UpdateBook(bookId, book);
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка перевода книги в архив!", ex);
        }
    }

    /// <inheritdoc cref="IBookService.GetBooks"/>
    public async Task<IList<BookListDto>> GetBooks(BookStatus status, BookCategory category, string author, int page,
        int pageSize)
    {
        try
        {
            var cacheKey = $"books:list:{HashCode.Combine(status, category, author, page, pageSize)}";
            var cache = await _redisService.GetAsync<IList<BookListDto>>(cacheKey);
            
            if (cache == null)
            {
                var books = await _bookRepository.GetBooks(status, category, author, page, pageSize);
                await _redisService.SetAsync(cacheKey, books, TimeSpan.FromMinutes(10));
                return books;
            }
            
            return cache;
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка получения списка книг!", ex);
        }
    }

    /// <inheritdoc cref="IBookService.CreateBookDetails"/>
    public async Task CreateBookDetails(Guid bookId, IFormFile coverImage, string description)
    {
        try
        {
            var book = await _bookRepository.GetBookById(bookId);
            
            // временная заглушка
            book.UpdateDetails(String.Empty, description);
            
            // todo добавление в MinIO
            
            // todo инвалидацию кэша деталей книг в Redis 
            
            await _bookRepository.UpdateBook(bookId, book);
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка добавления деталей книги!", ex);
        }
    }
}