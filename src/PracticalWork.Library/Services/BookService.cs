using Microsoft.AspNetCore.Http;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Input;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

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
            throw new BookServiceException("Ошибка создания книги.", ex);
        }
    }

    /// <inheritdoc cref="IBookService.UpdateBook"/>
    public async Task UpdateBook(Guid bookId, UpdateBookDto updateBookDto)
    {
        try
        {
            var book = await _bookRepository.GetBookById(bookId);

            if (book.IsArchived)
            {
                throw new ClientErrorException("Книга находится в архиве.");
            }

            await InvalidationBookListCache(book);
            await InvalidationLibraryBookCache(book);

            book.Title = updateBookDto.Title;
            book.Authors = updateBookDto.Authors;
            book.Year = updateBookDto.Year;

            await _bookRepository.UpdateBook(bookId, book);
        }
        catch (Exception ex) when (ex is not ClientErrorException)
        {
            throw new BookServiceException("Ошибка обновления данных книги!", ex);
        }
    }

    /// <inheritdoc cref="IBookService.ArchiveBook"/>
    public async Task<ArchiveBookDto> ArchiveBook(Guid bookId)
    {
        try
        {
            var book = await _bookRepository.GetBookById(bookId);

            if (!book.CanBeArchived())
            {
                throw new ClientErrorException("Книга не может быть переведена в архив.");
            }

            if (book.IsArchived)
            {
                throw new ClientErrorException("Книга уже переведена в архив.");
            }

            await InvalidationBookListCache(book);
            await InvalidationLibraryBookCache(book);

            book.Archive();

            await _bookRepository.UpdateBook(bookId, book);

            return new ArchiveBookDto(bookId, book.Title);
        }
        catch (Exception ex) when (ex is not ClientErrorException)
        {
            throw new BookServiceException("Ошибка перевода книги в архив!", ex);
        }
    }

    /// <inheritdoc cref="IBookService.GetBooks"/>
    public async Task<IList<BookListDto>> GetBooks(GetBookListDto getBookList)
    {
        try
        {
            var cacheKey = $"books:list:{HashCode.Combine(getBookList.Status, getBookList.Category, 
                getBookList.Author)}:{getBookList.Page}:{getBookList.PageSize}";
            var cache = await _redisService.GetAsync<IList<BookListDto>>(cacheKey);
            
            if (cache == null)
            {
                var books = await _bookRepository.GetBooks(getBookList.Status, getBookList.Category, 
                    getBookList.Author, getBookList.Page, getBookList.PageSize);

                foreach (var book in books)
                {
                    if (!string.IsNullOrWhiteSpace(book.CoverImagePath))
                    {
                        book.CoverImagePath = await _minIoService.GetFileUrlAsync(book.CoverImagePath);
                    }
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
        var extension = Path.GetExtension(coverImage.FileName);
        if (!IsValidImageExtension(extension) || coverImage.Length > 5 * 1024 * 1024)
        {
            throw new ClientErrorException("Неверный формат изображения!");
        }

        try
        {
            var book = await _bookRepository.GetBookById(bookId);
            if (book.IsArchived)
            {
                throw new ClientErrorException("Книга заархивирована.");
            }

            book.UpdateDetails(description,
                $"{DateTime.Today.Year}/{DateTime.Today.Month}/{bookId}{extension}");

            await _minIoService.UploadFileAsync(book.CoverImagePath, coverImage.OpenReadStream(), 
                coverImage.ContentType);
            
            await _bookRepository.UpdateBook(bookId, book);

            await _redisService.RemoveAsync($"book:details:{bookId}");
            await InvalidationBookListCache(book);
        }
        catch (Exception ex) when (ex is not ClientErrorException)
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