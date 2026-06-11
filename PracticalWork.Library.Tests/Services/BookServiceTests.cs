using Microsoft.AspNetCore.Http;
using Moq;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Input;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.MessageBroker.Events;
using PracticalWork.Library.Models;
using PracticalWork.Library.Services;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Tests.Services;

public class BookServiceTests
{
    private readonly Mock<IBookRepository> _bookRepositoryMock;
    private readonly Mock<IRedisService>   _redisServiceMock;
    private readonly Mock<IMinIoService>   _minIoServiceMock;
    private readonly Mock<IRabbitMqProducer> _producerMock;
    private readonly IBookService _bookService;

    private static readonly DateTimeOffset FakeNow =
        new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public BookServiceTests()
    {
        _bookRepositoryMock = new Mock<IBookRepository>();
        _redisServiceMock   = new Mock<IRedisService>();
        _minIoServiceMock   = new Mock<IMinIoService>();
        _producerMock       = new Mock<IRabbitMqProducer>();

        var timeProvider = new FakeTimeProvider(FakeNow);

        _bookService = new BookService(
            _bookRepositoryMock.Object,
            _redisServiceMock.Object,
            _minIoServiceMock.Object,
            _producerMock.Object,
            timeProvider
        );

        _producerMock
            .Setup(p => p.PublishEventAsync(It.IsAny<BaseEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Mock<IFormFile> CreateMockFormFile(
        string fileName, string contentType, long length)
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
        _bookRepositoryMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(0);

        // Act
        var resultId = await _bookService.CreateBook(book);

        // Assert
        Assert.Equal(expectedGuid, resultId);
        Assert.Equal(BookStatus.Available, book.Status);
        _bookRepositoryMock.Verify(r => r.CreateBook(book), Times.Once);
    }

    [Fact]
    public async Task CreateBook_WhenRepositoryFails_ShouldThrowBookServiceException()
    {
        // Arrange
        var book = new Book { Title = "Книга с ошибкой" };
        _bookRepositoryMock.Setup(r => r.CreateBook(book))
            .ThrowsAsync(new Exception("DB Error"));

        // Act and Assert
        var ex = await Assert.ThrowsAsync<BookServiceException>(
            () => _bookService.CreateBook(book));

        Assert.Equal("Ошибка создания книги.", ex.Message);
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
            Title      = "Старый заголовок",
            IsArchived = false,
            Category   = BookCategory.FictionBook,
            Authors    = new List<string> { "Старый Автор" },
            Status     = BookStatus.Available
        };

        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(existingBook);
        _bookRepositoryMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(0);
        _redisServiceMock.Setup(r => r.RemoveByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _bookService.UpdateBook(bookId, updateDto);

        // Assert
        _bookRepositoryMock.Verify(r => r.GetBookById(bookId), Times.Once);
        _redisServiceMock.Verify(r => r.RemoveByPrefixAsync(It.IsAny<string>()), Times.AtLeastOnce);
        _bookRepositoryMock.Verify(r => r.UpdateBook(bookId, It.Is<Book>(b =>
            b.Title   == updateDto.Title &&
            b.Authors == updateDto.Authors &&
            b.Year    == updateDto.Year
        )), Times.Once);
    }

    [Fact]
    public async Task UpdateBook_ShouldThrowClientErrorException_WhenBookIsArchived()
    {
        // Arrange
        var bookId     = Guid.NewGuid();
        var updateDto  = new UpdateBookDto("Новый заголовок", null, 2024);
        var archivedBook = new Book { IsArchived = true };

        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(archivedBook);

        // Act and Assert
        var ex = await Assert.ThrowsAsync<ClientErrorException>(
            () => _bookService.UpdateBook(bookId, updateDto));

        Assert.Equal("Книга находится в архиве.", ex.Message);
        _bookRepositoryMock.Verify(
            r => r.UpdateBook(It.IsAny<Guid>(), It.IsAny<Book>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveBook_ShouldArchiveBook_WhenCanBeArchived()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book
        {
            Title      = "Книга для архива",
            Status     = BookStatus.Available,
            IsArchived = false,
            Category   = BookCategory.ScientificBook,
            Authors    = new List<string> { "Автор" }
        };

        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(book);
        _bookRepositoryMock.Setup(r => r.UpdateBook(bookId, book)).Returns(Task.CompletedTask);
        _bookRepositoryMock.Setup(r => r.SaveChangesAsync()).ReturnsAsync(0);
        _redisServiceMock.Setup(r => r.RemoveByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _bookService.ArchiveBook(bookId);

        // Assert
        Assert.Equal(bookId, result.Id);
        Assert.Equal("Книга для архива", result.Title);
        Assert.True(book.IsArchived);
        Assert.Equal(BookStatus.Archived, book.Status);
        _bookRepositoryMock.Verify(r => r.UpdateBook(bookId, book), Times.Once);
        _redisServiceMock.Verify(r => r.RemoveByPrefixAsync(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ArchiveBook_ShouldThrowClientErrorException_WhenBookIsBorrowed()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Status = BookStatus.Borrow, IsArchived = false };

        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(book);

        // Act and Assert
        var ex = await Assert.ThrowsAsync<ClientErrorException>(
            () => _bookService.ArchiveBook(bookId));

        Assert.Equal("Книга не может быть переведена в архив.", ex.Message);
    }

    [Fact]
    public async Task ArchiveBook_ShouldThrowClientErrorException_WhenBookIsAlreadyArchived()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Status = BookStatus.Archived, IsArchived = true };

        _bookRepositoryMock.Setup(r => r.GetBookById(bookId)).ReturnsAsync(book);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ClientErrorException>(
            () => _bookService.ArchiveBook(bookId));

        Assert.Equal("Книга уже переведена в архив.", ex.Message);
    }

    [Fact]
    public async Task GetBooks_ShouldReturnFromCache_WhenCacheIsHit()
    {
        // Arrange
        var dto      = new GetBookListDto(BookStatus.Available, BookCategory.FictionBook, "Автор", 1, 10);
        var cacheKey = $"books:list:{HashCode.Combine(dto.Status, dto.Category, dto.Author)}:{dto.Page}:{dto.PageSize}";
        var cachedResult = new PagedListDto<BookListDto>(
            new List<BookListDto>
            {
                new(Guid.NewGuid(), "Книга из кэша", new List<string>(), "", 2020, "")
            }, 1, 10, 1);

        _redisServiceMock
            .Setup(r => r.GetAsync<PagedListDto<BookListDto>>(cacheKey))
            .ReturnsAsync(cachedResult);

        // Act
        var result = await _bookService.GetBooks(dto);

        // Assert
        Assert.Same(cachedResult, result);
        _bookRepositoryMock.Verify(r => r.GetBooks(
            It.IsAny<BookStatus>(), It.IsAny<BookCategory>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _minIoServiceMock.Verify(m => m.GetFileUrlAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetBooks_ShouldFetchFromDbAndSetCache_WhenCacheIsMiss()
    {
        // Arrange
        var dto      = new GetBookListDto(BookStatus.Available, BookCategory.FictionBook, "Автор", 1, 10);
        var cacheKey = $"books:list:{HashCode.Combine(dto.Status, dto.Category, dto.Author)}:{dto.Page}:{dto.PageSize}";

        var dbBooks = new PagedListDto<BookListDto>(
            new List<BookListDto>
            {
                new(Guid.NewGuid(), "Книга из БД", new List<string>(), "", 2020, "path/to/cover.jpg")
            }, dto.Page, dto.PageSize, 0);

        const string expectedUrl = "http://minio.url/path/to/cover.jpg";

        _redisServiceMock
            .Setup(r => r.GetAsync<PagedListDto<BookListDto>>(cacheKey))
            .ReturnsAsync((PagedListDto<BookListDto>)null!);

        _bookRepositoryMock
            .Setup(r => r.GetBooks(dto.Status, dto.Category, dto.Author, dto.Page, dto.PageSize))
            .ReturnsAsync(dbBooks);

        _minIoServiceMock
            .Setup(m => m.GetFileUrlAsync("path/to/cover.jpg", It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(expectedUrl);

        _redisServiceMock
            .Setup(r => r.SetAsync(cacheKey, It.IsAny<PagedListDto<BookListDto>>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _bookService.GetBooks(dto);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(expectedUrl, result.Items.First().CoverImagePath);
        _redisServiceMock.Verify(
            r => r.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10)), Times.Once);
    }

    [Theory]
    [InlineData("invalid.txt", "text/plain", 1024)]
    [InlineData("cover.png",   "image/png",  6 * 1024 * 1024)]
    public async Task CreateBookDetails_ShouldThrowClientErrorException_WhenFileIsInvalid(
        string fileName, string contentType, long length)
    {
        // Arrange
        var bookId   = Guid.NewGuid();
        var mockFile = CreateMockFormFile(fileName, contentType, length);

        // Act and Assert
        var ex = await Assert.ThrowsAsync<ClientErrorException>(
            () => _bookService.CreateBookDetails(bookId, mockFile.Object, "desc"));

        Assert.Equal("Неверный формат изображения!", ex.Message);
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;
}