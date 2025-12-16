using Moq;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Input;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Enums;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;
using PracticalWork.Library.Services;

namespace PracticalWork.Library.Tests.Services;

public class LibraryServiceTests
{
    private readonly Mock<ILibraryRepository> _libraryRepositoryMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IMinIoService> _minIoServiceMock;
    private readonly LibraryService _libraryService;

    public LibraryServiceTests()
    {
        _libraryRepositoryMock = new Mock<ILibraryRepository>();
        _redisServiceMock = new Mock<IRedisService>();
        _minIoServiceMock = new Mock<IMinIoService>();

        _libraryService = new LibraryService(
            _libraryRepositoryMock.Object,
            _redisServiceMock.Object,
            _minIoServiceMock.Object
        );
    }

    [Fact]
    public async Task BorrowBook_ShouldCreateBorrowRecordAndInvalidateCache()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var expectedBorrowId = Guid.NewGuid();
        var expectedBorrowDate = DateOnly.FromDateTime(DateTime.Now);
        var expectedDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30));

        _libraryRepositoryMock.Setup(r => r.CreateBorrow(It.IsAny<Borrow>()))
            .ReturnsAsync(expectedBorrowId);

        // Act
        var resultId = await _libraryService.BorrowBook(bookId, readerId);

        // Assert
        Assert.Equal(expectedBorrowId, resultId);

        _libraryRepositoryMock.Verify(r => r.CreateBorrow(It.Is<Borrow>(b =>
            b.BookId == bookId &&
            b.ReaderId == readerId &&
            b.BorrowDate == expectedBorrowDate &&
            b.DueDate == expectedDueDate &&
            b.Status == BookIssueStatus.Issued
        )), Times.Once);

        _redisServiceMock.Verify(r => r.RemoveAsync($"reader:books:{readerId}"), Times.Once);
    }

    [Fact]
    public async Task BorrowBook_WhenRepositoryFails_ShouldThrowLibraryServiceException()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        _libraryRepositoryMock.Setup(r => r.CreateBorrow(It.IsAny<Borrow>()))
            .ThrowsAsync(new InvalidOperationException("DB Error"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LibraryServiceException>(() =>
            _libraryService.BorrowBook(bookId, readerId));

        Assert.Equal("Ошибка создания записи выдачи книги!", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public async Task GetLibraryBooks_ShouldReturnFromCache_WhenCacheIsHit()
    {
        // Arrange
        var dto = new GetLibraryBooksDto(BookCategory.FictionBook, "Author", true, 1, 10);
        var cacheKey = $"library:books:{HashCode.Combine(dto.Category, dto.Author, dto.AvailableOnly)}:{dto.Page}:{dto.PageSize}";
        var cachedBooks = new List<LibraryBookDto> { new LibraryBookDto("Cached Book", new List<string>(), "", 2020) };

        _redisServiceMock.Setup(r => r.GetAsync<IList<LibraryBookDto>>(cacheKey)).ReturnsAsync(cachedBooks);

        // Act
        var result = await _libraryService.GetLibraryBooks(dto);

        // Assert
        Assert.Same(cachedBooks, result);
        _libraryRepositoryMock.Verify(r => r.GetLibraryBooks(It.IsAny<BookCategory>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetLibraryBooks_ShouldFetchFromDbAndSetCache_WhenCacheIsMiss()
    {
        // Arrange
        var dto = new GetLibraryBooksDto(BookCategory.FictionBook, "Author", true, 1, 10);
        var dbBooks = new List<LibraryBookDto> { new LibraryBookDto("DB Book", new List<string>(), "", 2020) };
    
        var cacheKey = $"library:books:{HashCode.Combine(dto.Category, dto.Author, dto.AvailableOnly)}:{dto.Page}:{dto.PageSize}";

        _redisServiceMock.Setup(r => r.GetAsync<IList<LibraryBookDto>>(cacheKey)).ReturnsAsync((IList<LibraryBookDto>)null!);
        _libraryRepositoryMock.Setup(r => r.GetLibraryBooks(dto.Category, dto.Author, dto.AvailableOnly, dto.Page, dto.PageSize))
            .ReturnsAsync(dbBooks);

        // Act
        var result = await _libraryService.GetLibraryBooks(dto);

        // Assert
        Assert.Same(dbBooks, result);
    
        _libraryRepositoryMock.Verify(r => r.GetLibraryBooks(dto.Category, dto.Author, dto.AvailableOnly, dto.Page, dto.PageSize), Times.Once);
    
        _redisServiceMock.Verify(r => r.SetAsync(
            cacheKey, 
            It.IsAny<IList<LibraryBookDto>>(),
            TimeSpan.FromMinutes(5)
        ), Times.Once);
    }

    [Fact]
    public async Task ReturnBook_ShouldCallReturnBookAndInvalidateCache()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var borrow = new Borrow
        {
            ReaderId = readerId,
            Status = BookIssueStatus.Issued,
            DueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7))
        };

        _libraryRepositoryMock.Setup(r => r.GetBorrowByBookId(bookId)).ReturnsAsync(borrow);

        // Act
        await _libraryService.ReturnBook(bookId);

        // Assert
        _libraryRepositoryMock.Verify(r => r.GetBorrowByBookId(bookId), Times.Once);

        _libraryRepositoryMock.Verify(r => r.UpdateBorrow(It.Is<Borrow>(b =>
            b.Status == BookIssueStatus.Returned || b.Status == BookIssueStatus.Overdue // Проверяем, что статус изменился
        )), Times.Once);
        
        _redisServiceMock.Verify(r => r.RemoveAsync($"reader:books:{readerId}"), Times.Once);
    }

    [Fact]
    public async Task ReturnBook_WhenBorrowReturnFails_ShouldThrowLibraryServiceException()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var borrow = new Borrow
        {
            ReaderId = Guid.NewGuid(),
            Status = BookIssueStatus.Returned
        };

        _libraryRepositoryMock.Setup(r => r.GetBorrowByBookId(bookId)).ReturnsAsync(borrow);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LibraryServiceException>(() =>
            _libraryService.ReturnBook(bookId));

        Assert.Equal("Ошибка возвращения книги библиотеки!", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        
        _libraryRepositoryMock.Verify(r => r.UpdateBorrow(It.IsAny<Borrow>()), Times.Never);
    }

    [Fact]
    public async Task GetBookDetailsById_ShouldReturnFromCache_WhenCacheIsHit()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var cacheKey = $"book:details:{bookId}";
        var cachedDetails = new BookDetailsDto(bookId, "Cached Title", new List<string>(), "", 2020, BookCategory.FictionBook, BookStatus.Available, "url", false);

        _redisServiceMock.Setup(r => r.GetAsync<BookDetailsDto>(cacheKey)).ReturnsAsync(cachedDetails);

        // Act
        var result = await _libraryService.GetBookDetailsById(bookId);

        // Assert
        Assert.Same(cachedDetails, result);
        _libraryRepositoryMock.Verify(r => r.GetBookById(It.IsAny<Guid>()), Times.Never);
        _minIoServiceMock.Verify(m => m.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetBookDetailsById_ShouldFetchFromDb_UpdateUrl_AndSetCache_WhenCacheMiss()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var cacheKey = $"book:details:{bookId}";
        var dbBook = new Book
        {
            Title = "DB Title",
            Authors = new List<string> { "Author" },
            Description = "Desc",
            Year = 2021,
            Category = BookCategory.ScientificBook,
            Status = BookStatus.Available,
            CoverImagePath = "minio/path/file.jpg",
            IsArchived = false
        };
        var expectedUrl = "http://minio.url/presigned";

        _redisServiceMock.Setup(r => r.GetAsync<BookDetailsDto>(cacheKey)).ReturnsAsync((BookDetailsDto)null!);
        _libraryRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(dbBook);
        _minIoServiceMock.Setup(m => m.GetFileUrlAsync(dbBook.CoverImagePath, It.IsAny<int>())).ReturnsAsync(expectedUrl);

        // Act
        var result = await _libraryService.GetBookDetailsById(bookId);

        // Assert
        Assert.Equal("DB Title", result.Title);
        Assert.Equal(expectedUrl, result.CoverImagePath);
        
        _redisServiceMock.Verify(r => r.SetAsync(cacheKey, It.Is<BookDetailsDto>(d => d.CoverImagePath == expectedUrl), TimeSpan.FromMinutes(30)), Times.Once);
    }

    [Fact]
    public async Task GetBookDetailsByTitle_ShouldCallRepositoryToGetId_AndThenGetDetails()
    {
        // Arrange
        var title = "The Title";
        var bookId = Guid.NewGuid();
        var expectedDetails = new BookDetailsDto(bookId, title, new List<string>(), "", 2020, BookCategory.FictionBook, BookStatus.Available, "url", false);
        
        _libraryRepositoryMock.Setup(r => r.GetBookIdByTitle(title)).ReturnsAsync(bookId);

        _redisServiceMock.Setup(r => r.GetAsync<BookDetailsDto>($"book:details:{bookId}")).ReturnsAsync((BookDetailsDto)null!);
        _libraryRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(new Book { Title = title, Authors = expectedDetails.Authors, Description = expectedDetails.Description, Year = expectedDetails.Year, Category = expectedDetails.Category, Status = expectedDetails.Status, CoverImagePath = "path", IsArchived = expectedDetails.IsArchived });
        _minIoServiceMock.Setup(m => m.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync("url");

        // Act
        var result = await _libraryService.GetBookDetailsByTitle(title);

        // Assert
        _libraryRepositoryMock.Verify(r => r.GetBookIdByTitle(title), Times.Once);
        _redisServiceMock.Verify(r => r.GetAsync<BookDetailsDto>($"book:details:{bookId}"), Times.Once);
        Assert.Equal(title, result.Title);
    }
    
    [Fact]
    public async Task GetBookDetailsByTitle_WhenGetBookIdFails_ShouldThrowLibraryServiceException()
    {
        // Arrange
        var title = "Missing Title";
        _libraryRepositoryMock.Setup(r => r.GetBookIdByTitle(title))
            .ThrowsAsync(new ArgumentException("Книга не найдена"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LibraryServiceException>(() =>
            _libraryService.GetBookDetailsByTitle(title));

        Assert.Equal("Ошибка получения деталей книги по названию!", ex.Message);
        Assert.IsType<ArgumentException>(ex.InnerException);
    }
}