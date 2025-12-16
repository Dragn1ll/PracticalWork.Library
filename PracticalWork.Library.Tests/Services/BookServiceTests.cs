using Microsoft.AspNetCore.Http;
using Moq;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Input;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;
using PracticalWork.Library.Services;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Tests.Services;

public class BookServiceTests
{
    private readonly Mock<IBookRepository> _bookRepositoryMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IMinIoService> _minIoServiceMock;
    private readonly IBookService _bookService;

    public BookServiceTests()
    {
        _bookRepositoryMock = new Mock<IBookRepository>();
        _redisServiceMock = new Mock<IRedisService>();
        _minIoServiceMock = new Mock<IMinIoService>();

        _bookService = new BookService(
            _bookRepositoryMock.Object,
            _redisServiceMock.Object,
            _minIoServiceMock.Object
        );
    }
    private Mock<IFormFile> CreateMockFormFile(string fileName, string contentType, long length)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());
        return mockFile;
    }

    [Fact]
    public async Task CreateBook_ShouldSetStatusToAvailableAndReturnId()
    {
        // Arrange
        var book = new Book { Title = "Новая книга" };
        var expectedGuid = Guid.NewGuid();
        _bookRepositoryMock.Setup(r => r.CreateBook(book)).ReturnsAsync(expectedGuid);

        // Act
        var resultId = await _bookService.CreateBook(book);

        // Assert
        Assert.Equal(expectedGuid, resultId);
        Assert.Equal(BookStatus.Available, book.Status); // Проверяем, что статус установлен [cite: 13]
        _bookRepositoryMock.Verify(r => r.CreateBook(book), Times.Once);
    }

    [Fact]
    public async Task CreateBook_WhenRepositoryFails_ShouldThrowBookServiceException()
    {
        // Arrange
        var book = new Book { Title = "Книга с ошибкой" };
        _bookRepositoryMock.Setup(r => r.CreateBook(book)).ThrowsAsync(new Exception("DB Error"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BookServiceException>(() => _bookService.CreateBook(book));
        Assert.Equal("Ошибка создания книги!", ex.Message);
        Assert.IsType<Exception>(ex.InnerException);
    }

    [Fact]
    public async Task UpdateBook_ShouldUpdateBook_WhenNotArchived()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var updateDto = new UpdateBookDto("Новый заголовок", new List<string> { "Новый Автор" }, 2024);
        var existingBook = new Book
        {
            Title = "Старый заголовок",
            IsArchived = false,
            Category = BookCategory.FictionBook,
            Authors = new List<string> { "Старый Автор" },
            Status = BookStatus.Available
        };
        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(existingBook);

        // Act
        await _bookService.UpdateBook(bookId, updateDto);

        // Assert
        _bookRepositoryMock.Verify(r => r.GetBookById(bookId), Times.Once);
        _redisServiceMock.Verify(r => r.RemoveByPrefixAsync(It.IsAny<string>()), Times.AtLeastOnce());
        // Проверка, что книга обновлена
        _bookRepositoryMock.Verify(r => r.UpdateBook(bookId, It.Is<Book>(b =>
            b.Title == updateDto.Title &&
            b.Authors == updateDto.Authors &&
            b.Year == updateDto.Year
        )), Times.Once);
    }

    [Fact]
    public async Task UpdateBook_ShouldThrowArgumentException_WhenBookIsArchived()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var updateDto = new UpdateBookDto("Новый заголовок", null, 2024);
        var archivedBook = new Book { IsArchived = true }; // [cite: 24]
        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(archivedBook);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BookServiceException>(() => _bookService.UpdateBook(bookId, updateDto));
        
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Equal("Книга находится в архиве.", ex.InnerException.Message);
        _bookRepositoryMock.Verify(r => r.UpdateBook(It.IsAny<Guid>(), It.IsAny<Book>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveBook_ShouldArchiveBook_WhenCanBeArchived()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book
        {
            Title = "Книга для архива",
            Status = BookStatus.Available, // CanBeArchived() вернет true
            IsArchived = false,
            Category = BookCategory.ScientificBook,
            Authors = new List<string> { "Автор" }
        };
        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(book);

        // Act
        var result = await _bookService.ArchiveBook(bookId);

        // Assert
        Assert.Equal(bookId, result.Id);
        Assert.Equal("Книга для архива", result.Title);
        Assert.True(book.IsArchived);
        Assert.Equal(BookStatus.Archived, book.Status);
        _bookRepositoryMock.Verify(r => r.UpdateBook(bookId, book), Times.Once);
        _redisServiceMock.Verify(r => r.RemoveByPrefixAsync(It.IsAny<string>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ArchiveBook_ShouldThrowArgumentException_WhenBookIsBorrowed()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book
        {
            Status = BookStatus.Borrow,
            IsArchived = false
        };
        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(book);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BookServiceException>(() => _bookService.ArchiveBook(bookId));
    
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Equal("Книга не может быть переведена в архив.", ex.InnerException.Message);
    }

    [Fact]
    public async Task ArchiveBook_ShouldThrowArgumentException_WhenBookIsAlreadyArchived()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book
        {
            Status = BookStatus.Archived,
            IsArchived = true
        };
        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(book);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BookServiceException>(() => _bookService.ArchiveBook(bookId));
    
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Equal("Книга уже переведена в архив.", ex.InnerException.Message);
    }

    [Fact]
    public async Task GetBooks_ShouldReturnFromCache_WhenCacheIsHit()
    {
        // Arrange
        var getBookListDto = new GetBookListDto(BookStatus.Available, BookCategory.FictionBook, "Автор", 1, 10);
        var cacheKey = $"books:list:{HashCode.Combine(getBookListDto.Status, getBookListDto.Category, getBookListDto.Author)}:{getBookListDto.Page}:{getBookListDto.PageSize}";
        var cachedBooks = new List<BookListDto>
        {
            new BookListDto(Guid.NewGuid(), "Книга из кэша", new List<string>(), "", 2020, "")
        };

        _redisServiceMock.Setup(r => r.GetAsync<IList<BookListDto>>(cacheKey)).ReturnsAsync(cachedBooks); // [cite: 31]

        // Act
        var result = await _bookService.GetBooks(getBookListDto);

        // Assert
        Assert.Same(cachedBooks, result);
        _bookRepositoryMock.Verify(r => r.GetBooks(It.IsAny<BookStatus>(), It.IsAny<BookCategory>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _minIoServiceMock.Verify(m => m.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetBooks_ShouldFetchFromDbAndSetCache_WhenCacheIsMiss()
    {
        // Arrange
        var getBookListDto = new GetBookListDto(BookStatus.Available, BookCategory.FictionBook, "Автор", 1, 10);
        var cacheKey = $"books:list:{HashCode.Combine(getBookListDto.Status, getBookListDto.Category, getBookListDto.Author)}:{getBookListDto.Page}:{getBookListDto.PageSize}";

        var dbBooks = new List<BookListDto>
        {
            new BookListDto(Guid.NewGuid(), "Книга из БД", new List<string>(), "", 2020, 
                "path/to/cover.jpg")
        };
        var expectedUrl = "http://minio.url/path/to/cover.jpg";

        _redisServiceMock.Setup(r => r.GetAsync<IList<BookListDto>>(cacheKey)).ReturnsAsync((IList<BookListDto>)null!);
        _bookRepositoryMock.Setup(r => r.GetBooks(getBookListDto.Status, getBookListDto.Category, getBookListDto.Author,
                getBookListDto.Page, getBookListDto.PageSize))
            .ReturnsAsync(dbBooks);
        _minIoServiceMock.Setup(m => m.GetFileUrlAsync("path/to/cover.jpg", It.IsAny<int>())).ReturnsAsync(expectedUrl);

        // Act
        var result = await _bookService.GetBooks(getBookListDto);

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedUrl, result.First().CoverImagePath);
        _redisServiceMock.Verify(r => r.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10)), Times.Once);
    }

    [Fact]
    public async Task CreateBookDetails_ShouldUpdateDetailsAndUploadFile_WhenInputIsValid()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var description = "Новое описание";
        var mockFile = CreateMockFormFile("cover.png", "image/png", 2 * 1024 * 1024);
        var book = new Book
        {
            Category = BookCategory.EducationalBook,
            Authors = new List<string> { "Автор" },
            Status = BookStatus.Available
        };
        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(book);

        // Act
        await _bookService.CreateBookDetails(bookId, mockFile.Object, description);

        // Assert
        _bookRepositoryMock.Verify(r => r.GetBookById(bookId), Times.Once);
        var expectedPath = $"{DateTime.Today.Year}/{DateTime.Today.Month}/{bookId}.png";
        Assert.Equal(expectedPath, book.CoverImagePath);
        Assert.Equal(description, book.Description);
        _minIoServiceMock.Verify(m => m.UploadFileAsync(expectedPath, It.IsAny<Stream>(), mockFile.Object.ContentType), Times.Once);
        // Проверяем обновление книги
        _bookRepositoryMock.Verify(r => r.UpdateBook(bookId, book), Times.Once);
        _redisServiceMock.Verify(r => r.RemoveAsync($"book:details:{bookId}"), Times.Once);
        _redisServiceMock.Verify(r => r.RemoveByPrefixAsync(It.IsAny<string>()), Times.AtLeastOnce());
    }

    [Theory]
    [InlineData("invalid.txt", "text/plain", 1024)]
    [InlineData("cover.png", "image/png", 6 * 1024 * 1024)]
    public async Task CreateBookDetails_ShouldThrowArgumentException_WhenFileIsInvalid(string fileName, string contentType, long length)
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var mockFile = CreateMockFormFile(fileName, contentType, length);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BookServiceException>(() =>
            _bookService.CreateBookDetails(bookId, mockFile.Object, "desc"));
    
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Equal("Неверный формат изображения!", ex.InnerException.Message);
    }
}