using PracticalWork.Library.Abstractions.Services;
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

    public LibraryService(IBookRepository bookRepository, IBorrowRepository borrowRepository)
    {
        _bookRepository = bookRepository;
        _borrowRepository = borrowRepository;
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
            return await _borrowRepository.CreateBorrow(borrow);
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
            // todo доставание данных из кэша Redis
            return await _bookRepository.GetLibraryBooks(category, author, availableOnly, page, pageSize);
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
            var book = await _bookRepository.GetBookById(bookId);
            // todo попытка достать данные из кэша Redis

            return new BookDetailsDto(book.Description, book.CoverImagePath);
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
            var book = await _bookRepository.GetBookById(bookId);
            // todo попытка достать данные из кэша Redis

            return new BookDetailsDto(book.Description, book.CoverImagePath);
        }
        catch (Exception ex)
        {
            throw new LibraryServiceException("Ошибка получения деталей книги по названию!", ex);
        }
    }
}