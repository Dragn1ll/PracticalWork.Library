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
    private readonly IMinIoService _minIoService;

    public BookService(IBookRepository bookRepository, IRedisService redisService, IMinIoService minIoService)
    {
        _bookRepository = bookRepository;
        _redisService = redisService;
        _minIoService = minIoService;
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
    public async Task UpdateBook(Guid bookId, Book newBook)
    {
        try
        {
            var book = await _bookRepository.GetBookById(bookId);

            if (book.IsArchived)
            {
                throw new ArgumentException("Книга находится в архиве.");
            }
            
            await InvalidationBookListCache(book);
            await InvalidationLibraryBookCache(book);
            
            await _bookRepository.UpdateBook(bookId, newBook);
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
            
            if (!book.CanBeArchived())
            {
                throw new ArgumentException("Книга не может быть переведена в архив.");
            }
            if (book.IsArchived)
            {
                throw new ArgumentException("Книга уже переведена в архив.");
            }
            
            await InvalidationBookListCache(book);
            await InvalidationLibraryBookCache(book);
            
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
            var cacheKey = $"books:list:{HashCode.Combine(status, category, author)}:{page}:{pageSize}";
            var cache = await _redisService.GetAsync<IList<BookListDto>>(cacheKey);
            
            if (cache == null)
            {
                var books = await _bookRepository.GetBooks(status, category, author, page, pageSize);

                foreach (var book in books)
                {
                    book.CoverImagePath = await _minIoService.GetFileUrlAsync(book.CoverImagePath);
                }
                
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
            if (!IsValidImageExtension(coverImage.ContentType) || coverImage.Length <= 5 * 1024 * 1024)
            {
                throw new ArgumentException("Неверный формат изображения!");
            }
            
            var book = await _bookRepository.GetBookById(bookId);

            book.UpdateDetails($"{DateTime.Today.Year}/{DateTime.Today.Month}/{bookId}.{coverImage.ContentType}",
                description);

            await _minIoService.UploadFileAsync(book.CoverImagePath, coverImage.OpenReadStream(), 
                coverImage.ContentType);
            
            await _bookRepository.UpdateBook(bookId, book);

            await _redisService.RemoveAsync($"book:details:{bookId}");
            await InvalidationBookListCache(book);
        }
        catch (Exception ex)
        {
            throw new BookServiceException("Ошибка добавления деталей книги!", ex);
        }
    }

    private async Task InvalidationBookListCache(Book book)
    {
        foreach (var author in book.Authors)
        {
            var prefix = $"books:list:{HashCode.Combine(book.Status, book.Category, author)}:";

            await _redisService.RemoveByPrefixAsync(prefix);
        }
    }

    private async Task InvalidationLibraryBookCache(Book book)
    {
        foreach (var author in book.Authors)
        {
            var prefix = $"books:list:{HashCode.Combine(book.Category, author, book.Status == BookStatus.Available)}:";

            await _redisService.RemoveByPrefixAsync(prefix);
        }
    }
    
    

    private bool IsValidImageExtension(string extension)
    {
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        return extension != null && allowedExtensions.Contains(extension);
    }
}