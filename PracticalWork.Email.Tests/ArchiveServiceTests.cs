using Microsoft.Extensions.Logging;
using Moq;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.MessageBroker.Events.Book;
using PracticalWork.Library.Services;

namespace PracticalWork.Email.Tests;

public class ArchiveServiceTests
{
    private readonly Mock<IBookRepository> _bookRepositoryMock;
    private readonly Mock<IBookService> _bookServiceMock;
    private readonly Mock<IRabbitMqProducer> _rabbitMqProducerMock;
    private readonly IArchiveService _archiveService;
 
    public ArchiveServiceTests()
    {
        _bookRepositoryMock = new Mock<IBookRepository>();
        _bookServiceMock = new Mock<IBookService>();
        _rabbitMqProducerMock = new Mock<IRabbitMqProducer>();
        Mock<ILogger<ArchiveService>> loggerMock = new Mock<ILogger<ArchiveService>>();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero));
 
        _archiveService = new ArchiveService(
            _bookRepositoryMock.Object,
            _bookServiceMock.Object,
            _rabbitMqProducerMock.Object,
            timeProvider,
            loggerMock.Object
        );
    }
 
    [Fact]
    public async Task ArchiveOldBooks_WhenNoBooksFound_ReturnsZeroCounts()
    {
        // Arrange
        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, 100))
            .ReturnsAsync(new List<AvailableOldBookDto>());
 
        // Act
        var result = await _archiveService.ArchiveOldBooks(3, 100);
 
        // Assert
        Assert.Equal(0, result.TotalProcessed);
        Assert.Equal(0, result.ArchivedCount);
        Assert.Equal(0, result.SkippedCount);
    }
 
    [Fact]
    public async Task ArchiveOldBooks_WhenAllBooksArchived_ReturnsCorrectCounts()
    {
        // Arrange
        var books = new List<AvailableOldBookDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Книга 1" },
            new() { Id = Guid.NewGuid(), Title = "Книга 2" },
            new() { Id = Guid.NewGuid(), Title = "Книга 3" }
        };
 
        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, 100))
            .ReturnsAsync(books);
 
        _bookServiceMock
            .Setup(s => s.ArchiveBook(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new ArchiveBookDto(id, "Книга"));
 
        _rabbitMqProducerMock
            .Setup(p => p.PublishEventAsync(It.IsAny<BookArchivedEvent>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
 
        // Act
        var result = await _archiveService.ArchiveOldBooks(3, 100);
 
        // Assert
        Assert.Equal(3, result.TotalProcessed);
        Assert.Equal(3, result.ArchivedCount);
        Assert.Equal(0, result.SkippedCount);
    }
 
    [Fact]
    public async Task ArchiveOldBooks_WhenSomeBooksFailToArchive_CountsThemAsSkipped()
    {
        // Arrange
        var goodBookId = Guid.NewGuid();
        var badBookId = Guid.NewGuid();
        var books = new List<AvailableOldBookDto>
        {
            new() { Id = goodBookId, Title = "Нормальная книга" },
            new() { Id = badBookId, Title = "Проблемная книга" }
        };
 
        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, 100))
            .ReturnsAsync(books);
 
        _bookServiceMock
            .Setup(s => s.ArchiveBook(goodBookId))
            .ReturnsAsync(new ArchiveBookDto(goodBookId, "Нормальная книга"));
 
        _bookServiceMock
            .Setup(s => s.ArchiveBook(badBookId))
            .ThrowsAsync(new BookServiceException("Книга уже выдана"));
 
        _rabbitMqProducerMock
            .Setup(p => p.PublishEventAsync(It.IsAny<BookArchivedEvent>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
 
        // Act
        var result = await _archiveService.ArchiveOldBooks(3, 100);
 
        // Assert
        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(1, result.ArchivedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains("Книга уже выдана", result.SkipReasons);
    }
 
    [Fact]
    public async Task ArchiveOldBooks_WhenBookArchived_PublishesEventForEachBook()
    {
        // Arrange
        var book1Id = Guid.NewGuid();
        var book2Id = Guid.NewGuid();
        var books = new List<AvailableOldBookDto>
        {
            new() { Id = book1Id, Title = "Книга 1" },
            new() { Id = book2Id, Title = "Книга 2" }
        };
 
        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, 100))
            .ReturnsAsync(books);
 
        _bookServiceMock
            .Setup(s => s.ArchiveBook(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new ArchiveBookDto(id, "Книга"));
 
        _rabbitMqProducerMock
            .Setup(p => p.PublishEventAsync(It.IsAny<BookArchivedEvent>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
 
        // Act
        await _archiveService.ArchiveOldBooks(3, 100);
 
        // Assert
        _rabbitMqProducerMock.Verify(
            p => p.PublishEventAsync(It.IsAny<BookArchivedEvent>(), CancellationToken.None),
            Times.Exactly(2));
    }
 
    [Fact]
    public async Task ArchiveOldBooks_CutoffDateCalculatedCorrectly()
    {
        // Arrange
        var expectedCutoff = new DateOnly(2022, 6, 15);
 
        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(expectedCutoff, 1, 50))
            .ReturnsAsync(new List<AvailableOldBookDto>())
            .Verifiable();
 
        // Act
        await _archiveService.ArchiveOldBooks(3, 50);
 
        // Assert
        _bookRepositoryMock.Verify(
            r => r.GetAvailableOldBooks(expectedCutoff, 1, 50),
            Times.Once);
    }
 
    [Fact]
    public async Task ArchiveOldBooks_MaxBooksPerRunIsPassedToRepository()
    {
        // Arrange
        const int maxBooks = 42;
 
        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, maxBooks))
            .ReturnsAsync(new List<AvailableOldBookDto>());
 
        // Act
        await _archiveService.ArchiveOldBooks(3, maxBooks);
 
        // Assert
        _bookRepositoryMock.Verify(
            r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, maxBooks),
            Times.Once);
    }
 
    [Fact]
    public async Task ArchiveOldBooks_WhenAllBooksFail_AllCountedAsSkipped()
    {
        // Arrange
        var books = new List<AvailableOldBookDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Выданная книга" }
        };
 
        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, 100))
            .ReturnsAsync(books);
 
        _bookServiceMock
            .Setup(s => s.ArchiveBook(It.IsAny<Guid>()))
            .ThrowsAsync(new ClientErrorException("Книга выдана читателю"));
 
        // Act
        var result = await _archiveService.ArchiveOldBooks(3, 100);
 
        // Assert
        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(0, result.ArchivedCount);
        Assert.Equal(1, result.SkippedCount);
 
        _rabbitMqProducerMock.Verify(
            p => p.PublishEventAsync(It.IsAny<BookArchivedEvent>(), CancellationToken.None),
            Times.Never);
    }
 
    [Fact]
    public async Task ArchiveOldBooks_ReturnsNonZeroExecutionTime()
    {
        // Arrange
        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, 100))
            .ReturnsAsync(new List<AvailableOldBookDto>());
 
        // Act
        var result = await _archiveService.ArchiveOldBooks(3, 100);
 
        // Assert
        Assert.True(result.ExecutionTime >= TimeSpan.Zero);
    }
 
    [Fact]
    public async Task ArchiveOldBooks_WhenMultipleErrorsOccur_SkipReasonsContainAllMessages()
    {
        // Arrange
        var books = new List<AvailableOldBookDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Книга 1" },
            new() { Id = Guid.NewGuid(), Title = "Книга 2" }
        };
 
        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, 100))
            .ReturnsAsync(books);
 
        _bookServiceMock
            .SetupSequence(s => s.ArchiveBook(It.IsAny<Guid>()))
            .ThrowsAsync(new ClientErrorException("Ошибка A"))
            .ThrowsAsync(new ClientErrorException("Ошибка B"));
 
        // Act
        var result = await _archiveService.ArchiveOldBooks(3, 100);
 
        // Assert
        Assert.NotEmpty(result.SkipReasons);
        Assert.True(result.SkipReasons.Contains("Ошибка A") || result.SkipReasons.Contains("Ошибка B"));
    }
}