using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto;
using PracticalWork.Library.Dto.Input;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.MessageBroker.Events.Book;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Services;

/// <inheritdoc cref="ILibraryService"/>
public sealed class LibraryService : ILibraryService
{
    private readonly ILibraryRepository _libraryRepository;
    private readonly IRedisService _redisService;
    private readonly IMinIoService _minIoService;
    private readonly IRabbitMqProducer _producer;

    public LibraryService(ILibraryRepository libraryRepository, IRedisService redisService, IMinIoService minIoService,
        IRabbitMqProducer producer)
    {
        _libraryRepository = libraryRepository;
        _redisService = redisService;
        _minIoService = minIoService;
        _producer = producer;
    }
    
    /// <inheritdoc cref="ILibraryService.BorrowBook"/>
    public async Task<Guid> BorrowBook(Guid bookId, Guid readerId)
    {
        try
        {
            var book = await _libraryRepository.GetBookById(bookId);
            if (!book.CanBeBorrowed())
            {
                throw new ClientErrorException("Книга не может быть выдана.");
            }
            
            var borrow = new Borrow
            {
                BookId = bookId,
                ReaderId = readerId,
                BorrowDate = DateOnly.FromDateTime(DateTime.Now),
                DueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
                Status = BookIssueStatus.Issued
            };
            
            var borrowId = await _libraryRepository.CreateBorrow(borrow);
            
            await _redisService.RemoveAsync($"reader:books:{borrow.ReaderId}");
            
            await _producer.PublishEventAsync(new BookBorrowedEvent(bookId, readerId, book.Title, "", 
                borrow.BorrowDate.ToDateTime(default), borrow.DueDate.ToDateTime(default)));
            
            return borrowId;
        }
        catch (Exception ex) when (ex is not ClientErrorException)
        {
            throw new LibraryServiceException("Ошибка создания записи выдачи книги!", ex);
        }
    }

    /// <inheritdoc cref="ILibraryService.GetLibraryBooks"/>
    public async Task<IList<LibraryBookDto>> GetLibraryBooks(GetLibraryBooksDto getLibraryBooksDto)
    {
        try
        {
            var cacheKey = $"library:books:{HashCode.Combine(getLibraryBooksDto.Category, getLibraryBooksDto.Author, 
                getLibraryBooksDto.AvailableOnly)}:{getLibraryBooksDto.Page}:{getLibraryBooksDto.PageSize}";
            var cache = await _redisService.GetAsync<IList<LibraryBookDto>>(cacheKey);

            if (cache == null)
            {
                var libraryBooks = await _libraryRepository
                    .GetLibraryBooks(getLibraryBooksDto.Category, getLibraryBooksDto.Author, getLibraryBooksDto.AvailableOnly, 
                        getLibraryBooksDto.Page, getLibraryBooksDto.PageSize);
                
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
            var borrow = await _libraryRepository.GetBorrowByBookId(bookId);
            
            borrow.ReturnBook();
            
            await _libraryRepository.UpdateBorrow(borrow);

            await _redisService.RemoveAsync($"reader:books:{borrow.ReaderId}");
            
            await _producer.PublishEventAsync(new BookReturnedEvent(bookId, borrow.ReaderId, "", "", 
                borrow.ReturnDate.ToDateTime(default)));
        }
        catch (Exception ex) when (ex is not ClientErrorException)
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
                var book = await _libraryRepository.GetBookById(bookId);
                if (book.IsArchived)
                {
                    throw new ClientErrorException("Книга заархивирована.");
                }
                
                var coverImagePath = string.IsNullOrEmpty(book.CoverImagePath) 
                    ? null
                    : await _minIoService.GetFileUrlAsync(book.CoverImagePath);
                var bookDetails = new BookDetailsDto(bookId, book.Title, book.Authors, book.Description, book.Year, 
                    book.Category, book.Status, coverImagePath, book.IsArchived);
                
                await _redisService.SetAsync(cacheKey, bookDetails, TimeSpan.FromMinutes(30));

                return bookDetails;
            }

            return cache;
        }
        catch (Exception ex) when (ex is not ClientErrorException)
        {
            throw new LibraryServiceException("Ошибка получения деталей книги по идентификатору!", ex);
        }
    }

    /// <inheritdoc cref="ILibraryService.GetBookDetailsByTitle"/>
    public async Task<BookDetailsDto> GetBookDetailsByTitle(string title)
    {
        try
        {
            var bookId = await _libraryRepository.GetBookIdByTitle(title);
            
            return await GetBookDetailsById(bookId);
        }
        catch (Exception ex) when (ex is not ClientErrorException)
        {
            throw new LibraryServiceException("Ошибка получения деталей книги по названию!", ex);
        }
    }
}