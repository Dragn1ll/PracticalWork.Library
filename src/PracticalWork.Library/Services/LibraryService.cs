using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;

namespace PracticalWork.Library.Services;

/// <inheritdoc cref="ILibraryService"/>
public sealed class LibraryService : ILibraryService
{
    private readonly IBookRepository _bookRepository;
    private readonly IBorrowRepository _borrowRepository;
    private readonly IRedisService _redisService;

    public LibraryService(IBookRepository bookRepository, IBorrowRepository borrowRepository, IRedisService redisService)
    {
        _bookRepository = bookRepository;
        _borrowRepository = borrowRepository;
        _redisService = redisService;
    }
    
    /// <inheritdoc cref="ILibraryService.BorrowBook"/>
    public async Task<Guid> BorrowBook(Guid bookId, Guid readerId)
    {
        var borrow = new Borrow
        {
            BookId = bookId,
            ReaderId = readerId,
            BorrowDate = DateOnly.FromDateTime(DateTime.Now),
            DueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Status = BookIssueStatus.Issued
        };

        try
        {
            var borrowId = await _borrowRepository.CreateBorrow(borrow);
            
            await _redisService.RemoveAsync($"reader:books:{borrow.ReaderId}");
            
            return borrowId;
        }
        catch (Exception ex)
        {
            throw new LibraryServiceException("Ошибка создания записи выдачи книги!", ex);
        }
    }

    /// <inheritdoc cref="ILibraryService.GetLibraryBooks"/>
    public async Task<IList<LibraryBookDto>> GetLibraryBooks(BookCategory category, string author, bool availableOnly, 
        int page, int pageSize)
    {
        try
        {
            var cacheKey = $"library:books:{HashCode.Combine(category, author, availableOnly)}:{page}:{pageSize}";
            var cache = await _redisService.GetAsync<IList<LibraryBookDto>>(cacheKey);

            if (cache == null)
            {
                var libraryBooks = await _bookRepository
                    .GetLibraryBooks(category, author, availableOnly, page, pageSize);
                
                await _redisService.SetAsync(cacheKey, libraryBooks, TimeSpan.FromMinutes(5));
                
                return libraryBooks;
            }
            
            return cache;
        }
        catch (Exception ex)
        {
            throw new LibraryServiceException("Ошибка получения книг библиотеки!", ex);
        }
    }

    /// <inheritdoc cref="ILibraryService.ReturnBook"/>
    public async Task ReturnBook(Guid bookId)
    {
        try
        {
            var borrow = await _borrowRepository.GetBorrowByBookId(bookId);
            
            borrow.ReturnBook();
            
            await _borrowRepository.UpdateBorrow(borrow);

            await _redisService.RemoveAsync($"reader:books:{borrow.ReaderId}");
        }
        catch (Exception ex)
        {
            throw new LibraryServiceException("Ошибка возвращения книги библиотеки!", ex);
        }
    }

    /// <inheritdoc cref="ILibraryService.GetBookDetailsById"/>
    public async Task<BookDetailsDto> GetBookDetailsById(Guid bookId)
    {
        try
        {
            var cacheKey = $"book:details:{bookId}";
            var cache = await _redisService.GetAsync<BookDetailsDto>(cacheKey);

            if (cache == null)
            {
                var book = await _bookRepository.GetBookById(bookId);
                var bookDetails = new BookDetailsDto(book.Description, book.CoverImagePath);
                
                await _redisService.SetAsync(cacheKey, bookDetails, TimeSpan.FromMinutes(30));

                return bookDetails;
            }

            return cache;
        }
        catch (Exception ex)
        {
            throw new LibraryServiceException("Ошибка получения деталей книги по идентификатору!", ex);
        }
    }

    /// <inheritdoc cref="ILibraryService.GetBookDetailsByTitle"/>
    public async Task<BookDetailsDto> GetBookDetailsByTitle(string title)
    {
        try
        {
            var bookId = await _bookRepository.GetBookIdByTitle(title);
            
            return await GetBookDetailsById(bookId);
        }
        catch (Exception ex)
        {
            throw new LibraryServiceException("Ошибка получения деталей книги по названию!", ex);
        }
    }
}