using Moq;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;
using PracticalWork.Library.Services;

namespace PracticalWork.Library.Tests.Services;

public class ReaderServiceTests
{
    private readonly Mock<IReaderRepository> _readerRepositoryMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IRabbitMqProducer> _producerMock;
    private readonly ReaderService _readerService;

    public ReaderServiceTests()
    {
        _readerRepositoryMock = new Mock<IReaderRepository>();
        _redisServiceMock = new Mock<IRedisService>();
        _producerMock = new Mock<IRabbitMqProducer>();
        
        _readerService = new ReaderService(_readerRepositoryMock.Object, _redisServiceMock.Object, 
            _producerMock.Object);
    }

    [Fact]
    public async Task CreateReader_ShouldSetActiveAndReturnId_WhenSuccessful()
    {
        // Arrange
        var reader = new Reader { IsActive = false, PhoneNumber = "123" };
        var expectedId = Guid.NewGuid();
        _readerRepositoryMock.Setup(r => r.CreateReader(reader)).ReturnsAsync(expectedId);

        // Act
        var resultId = await _readerService.CreateReader(reader);

        // Assert
        Assert.True(reader.IsActive);
        Assert.Equal(expectedId, resultId);
        _readerRepositoryMock.Verify(r => r.CreateReader(reader), Times.Once);
    }

    [Fact]
    public async Task CreateReader_WhenPhoneNumberExists_ShouldThrowReaderServiceException()
    {
        // Arrange
        var reader = new Reader { PhoneNumber = "123" };
        _readerRepositoryMock.Setup(r => r.CreateReader(reader))
            .ThrowsAsync(new ArgumentException("Читатель с таким номером телефона уже существует"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ReaderServiceException>(() => _readerService.CreateReader(reader));
        
        Assert.Equal("Ошибка создания карточки читателя!", ex.Message);
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Contains("Читатель с таким номером телефона уже существует", ex.InnerException.Message);
    }

    [Fact]
    public async Task ExtendValidity_ShouldUpdateExpiryDateAndSave_WhenInputIsValid()
    {
        // Arrange
        var readerId = Guid.NewGuid();
        var newExpiryDate = DateOnly.FromDateTime(DateTime.Now.Date).AddYears(1);
        var reader = new Reader { IsActive = true, ExpiryDate = DateOnly.FromDateTime(DateTime.Now.Date).AddDays(1) };
        _readerRepositoryMock.Setup(r => r.GetReaderById(readerId)).ReturnsAsync(reader);

        // Act
        await _readerService.ExtendValidity(readerId, newExpiryDate);

        // Assert
        Assert.Equal(newExpiryDate, reader.ExpiryDate);
        _readerRepositoryMock.Verify(r => r.UpdateReader(readerId, reader), Times.Once);
    }

    [Fact]
    public async Task ExtendValidity_ShouldThrowReaderServiceException_WhenNewDateIsInPast()
    {
        // Arrange
        var readerId = Guid.NewGuid();
        var pastDate = DateOnly.FromDateTime(DateTime.Now.Date).AddDays(-1);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ReaderServiceException>(() => _readerService.ExtendValidity(readerId, pastDate));

        Assert.Equal("Ошибка продления карточки читателя!", ex.Message);
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Equal("Дата продления не может быть раньше сегодняшней!", ex.InnerException.Message);
        _readerRepositoryMock.Verify(r => r.GetReaderById(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ExtendValidity_ShouldThrowReaderServiceException_WhenCardIsNotActiveOrExpired()
    {
        // Arrange
        var readerId = Guid.NewGuid();
        var newExpiryDate = DateOnly.FromDateTime(DateTime.Now.Date).AddYears(1);
        var reader = new Reader { IsActive = true, ExpiryDate = DateOnly.FromDateTime(DateTime.Now.Date).AddDays(-1) }; 
        _readerRepositoryMock.Setup(r => r.GetReaderById(readerId)).ReturnsAsync(reader);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ReaderServiceException>(() => _readerService.ExtendValidity(readerId, newExpiryDate));
        
        Assert.Equal("Ошибка продления карточки читателя!", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal("Карточка не может быть продлена!", ex.InnerException.Message);
        _readerRepositoryMock.Verify(r => r.UpdateReader(It.IsAny<Guid>(), It.IsAny<Reader>()), Times.Never);
    }
    
    [Fact]
    public async Task ExtendValidity_WhenReaderNotFound_ShouldThrowReaderServiceException()
    {
        // Arrange
        var readerId = Guid.NewGuid();
        var newExpiryDate = DateOnly.FromDateTime(DateTime.Now.Date).AddYears(1);
        _readerRepositoryMock.Setup(r => r.GetReaderById(readerId))
            .ThrowsAsync(new ArgumentException("Отсутствует читатель"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ReaderServiceException>(() => _readerService.ExtendValidity(readerId, newExpiryDate));
        
        Assert.Equal("Ошибка продления карточки читателя!", ex.Message);
        Assert.IsType<ArgumentException>(ex.InnerException);
    }
    
    [Fact]
    public async Task CloseReader_ShouldDeactivateReader_WhenNoBooksBorrowed()
    {
        // Arrange
        var readerId = Guid.NewGuid();
        var reader = new Reader { IsActive = false }; 
        
        _readerRepositoryMock.Setup(r => r.GetReaderById(readerId)).ReturnsAsync(reader);
        _readerRepositoryMock.Setup(r => r.GetBorrowedBooks(readerId)).ReturnsAsync(new List<BorrowedBookDto>());
        
        _redisServiceMock.Setup(r => r.GetAsync<IList<BorrowedBookDto>>($"reader:books:{readerId}")).ReturnsAsync((IList<BorrowedBookDto>)null!);

        // Act
        await _readerService.CloseReader(readerId);

        // Assert
        Assert.False(reader.IsActive);
        _readerRepositoryMock.Verify(r => r.UpdateReader(readerId, reader), Times.Once);
    }

    [Fact]
    public async Task CloseReader_ShouldThrowException_WhenCardIsActiveDueToModelLogic()
    {
        // Arrange
        var readerId = Guid.NewGuid();
        var reader = new Reader { IsActive = true }; 
        
        _readerRepositoryMock.Setup(r => r.GetReaderById(readerId)).ReturnsAsync(reader);
        _redisServiceMock.Setup(r => r.GetAsync<IList<BorrowedBookDto>>($"reader:books:{readerId}")).ReturnsAsync((IList<BorrowedBookDto>)null!);
        _readerRepositoryMock.Setup(r => r.GetBorrowedBooks(readerId)).ReturnsAsync(new List<BorrowedBookDto>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ReaderServiceException>(() => _readerService.CloseReader(readerId));

        Assert.Equal("Ошибка закрытия карточки читателя!", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal("Карточка не может быть закрыта, так как уже закрыта!", ex.InnerException.Message);
        Assert.True(reader.IsActive);
    }

    [Fact]
    public async Task CloseReader_ShouldThrowReaderServiceException_WhenBooksAreBorrowed()
    {
        // Arrange
        var readerId = Guid.NewGuid();
        var reader = new Reader { IsActive = false };
        var borrowedBooks = new List<BorrowedBookDto> { new BorrowedBookDto(Guid.NewGuid(), DateOnly.MinValue, DateOnly.MaxValue) };
        
        _readerRepositoryMock.Setup(r => r.GetReaderById(readerId)).ReturnsAsync(reader);
        _readerRepositoryMock.Setup(r => r.GetBorrowedBooks(readerId)).ReturnsAsync(borrowedBooks);
        _redisServiceMock.Setup(r => r.GetAsync<IList<BorrowedBookDto>>($"reader:books:{readerId}")).ReturnsAsync((IList<BorrowedBookDto>)null!);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ReaderServiceException>(() => _readerService.CloseReader(readerId));
        
        Assert.Equal("Ошибка закрытия карточки читателя!", ex.Message);
        
        _readerRepositoryMock.Verify(r => r.UpdateReader(It.IsAny<Guid>(), It.IsAny<Reader>()), Times.Never);
    }

    [Fact]
    public async Task GetBorrowedBooks_ShouldReturnFromCache_WhenCacheIsHit()
    {
        // Arrange
        var readerId = Guid.NewGuid();
        var cacheKey = $"reader:books:{readerId}";
        var cachedBooks = new List<BorrowedBookDto> { new BorrowedBookDto(Guid.NewGuid(), DateOnly.MinValue, DateOnly.MaxValue) };
        
        _redisServiceMock.Setup(r => r.GetAsync<IList<BorrowedBookDto>>(cacheKey)).ReturnsAsync(cachedBooks);

        // Act
        var result = await _readerService.GetBorrowedBooks(readerId);

        // Assert
        Assert.Same(cachedBooks, result);
        _readerRepositoryMock.Verify(r => r.GetBorrowedBooks(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetBorrowedBooks_ShouldFetchFromDbAndSetCache_WhenCacheIsMiss()
    {
        // Arrange
        var readerId = Guid.NewGuid();
        var cacheKey = $"reader:books:{readerId}";
        var dbBooks = new List<BorrowedBookDto> { new BorrowedBookDto(Guid.NewGuid(), DateOnly.MinValue, DateOnly.MaxValue) };
        
        _redisServiceMock.Setup(r => r.GetAsync<IList<BorrowedBookDto>>(cacheKey)).ReturnsAsync((IList<BorrowedBookDto>)null!);
        _readerRepositoryMock.Setup(r => r.GetBorrowedBooks(readerId)).ReturnsAsync(dbBooks);

        // Act
        var result = await _readerService.GetBorrowedBooks(readerId);

        // Assert
        Assert.Same(dbBooks, result);
        _readerRepositoryMock.Verify(r => r.GetBorrowedBooks(readerId), Times.Once);
        _redisServiceMock.Verify(r => r.SetAsync(
            cacheKey, 
            It.IsAny<IList<BorrowedBookDto>>(), 
            TimeSpan.FromMinutes(15)
        ), Times.Once);
    }
}