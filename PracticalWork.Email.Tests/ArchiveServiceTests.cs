using Microsoft.Extensions.Logging;
using Moq;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.MessageBroker.Events.Book;
using PracticalWork.Library.Models;
using PracticalWork.Library.Services;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Email.Tests;

public class ArchiveServiceTests
{
    private readonly Mock<IBookRepository> _bookRepositoryMock;
    private readonly Mock<IRabbitMqProducer> _rabbitMqProducerMock;
    private readonly IArchiveService _archiveService;

    private static readonly DateTimeOffset FakeNow =
        new(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);

    public ArchiveServiceTests()
    {
        _bookRepositoryMock    = new Mock<IBookRepository>();
        _rabbitMqProducerMock  = new Mock<IRabbitMqProducer>();
        Mock<ILogger<ArchiveService>> loggerMock = new();

        var timeProvider = new FakeTimeProvider(FakeNow);

        _archiveService = new ArchiveService(
            _bookRepositoryMock.Object,
            _rabbitMqProducerMock.Object,
            timeProvider,
            loggerMock.Object
        );

        _rabbitMqProducerMock
            .Setup(p => p.PublishEventAsync(It.IsAny<BookArchivedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Book CreateArchivableBook(string title = "Книга") =>
        new() { Title = title, Status = BookStatus.Available };

    private static Book CreateBorrowedBook(string title = "Выданная книга") =>
        new() { Title = title, Status = BookStatus.Borrow };
    
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

        _bookRepositoryMock
            .Setup(r => r.GetBookById(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => CreateArchivableBook());

        _bookRepositoryMock
            .Setup(r => r.UpdateBook(It.IsAny<Guid>(), It.IsAny<Book>()))
            .Returns(Task.CompletedTask);

        _bookRepositoryMock
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(0);

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
        var badBookId  = Guid.NewGuid();

        var books = new List<AvailableOldBookDto>
        {
            new() { Id = goodBookId, Title = "Нормальная книга" },
            new() { Id = badBookId,  Title = "Проблемная книга" }
        };

        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, 100))
            .ReturnsAsync(books);

        _bookRepositoryMock
            .Setup(r => r.GetBookById(goodBookId))
            .ReturnsAsync(CreateArchivableBook("Нормальная книга"));

        _bookRepositoryMock
            .Setup(r => r.GetBookById(badBookId))
            .ReturnsAsync(CreateBorrowedBook("Проблемная книга"));

        _bookRepositoryMock
            .Setup(r => r.UpdateBook(It.IsAny<Guid>(), It.IsAny<Book>()))
            .Returns(Task.CompletedTask);

        _bookRepositoryMock
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(0);

        // Act
        var result = await _archiveService.ArchiveOldBooks(3, 100);

        // Assert
        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(1, result.ArchivedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.NotEmpty(result.SkipReasons);
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

        _bookRepositoryMock
            .Setup(r => r.GetBookById(It.IsAny<Guid>()))
            .ReturnsAsync((Guid _) => CreateArchivableBook());

        _bookRepositoryMock
            .Setup(r => r.UpdateBook(It.IsAny<Guid>(), It.IsAny<Book>()))
            .Returns(Task.CompletedTask);

        _bookRepositoryMock
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(0);

        // Act
        await _archiveService.ArchiveOldBooks(3, 100);

        // Assert
        _rabbitMqProducerMock.Verify(
            p => p.PublishEventAsync(It.IsAny<BookArchivedEvent>(), It.IsAny<CancellationToken>()),
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
        var bookId = Guid.NewGuid();
        var books = new List<AvailableOldBookDto>
        {
            new() { Id = bookId, Title = "Выданная книга" }
        };

        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, 100))
            .ReturnsAsync(books);

        _bookRepositoryMock
            .Setup(r => r.GetBookById(bookId))
            .ReturnsAsync(CreateBorrowedBook("Выданная книга"));

        _bookRepositoryMock
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(0);

        // Act
        var result = await _archiveService.ArchiveOldBooks(3, 100);

        // Assert
        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(0, result.ArchivedCount);
        Assert.Equal(1, result.SkippedCount);

        _rabbitMqProducerMock.Verify(
            p => p.PublishEventAsync(It.IsAny<BookArchivedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ArchiveOldBooks_ReturnsNonNegativeExecutionTime()
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

        _bookRepositoryMock
            .Setup(r => r.GetBookById(It.IsAny<Guid>()))
            .ReturnsAsync(CreateBorrowedBook());

        _bookRepositoryMock
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(0);

        // Act
        var result = await _archiveService.ArchiveOldBooks(3, 100);

        // Assert
        Assert.Equal(2, result.SkippedCount);
        Assert.NotEmpty(result.SkipReasons);
    }

    [Fact]
    public async Task ArchiveOldBooks_WhenBookArchived_CallsUpdateBookOnRepository()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var books = new List<AvailableOldBookDto>
        {
            new() { Id = bookId, Title = "Книга" }
        };

        _bookRepositoryMock
            .Setup(r => r.GetAvailableOldBooks(It.IsAny<DateOnly>(), 1, 100))
            .ReturnsAsync(books);

        _bookRepositoryMock
            .Setup(r => r.GetBookById(bookId))
            .ReturnsAsync(CreateArchivableBook("Книга"));

        _bookRepositoryMock
            .Setup(r => r.UpdateBook(It.IsAny<Guid>(), It.IsAny<Book>()))
            .Returns(Task.CompletedTask);

        _bookRepositoryMock
            .Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(0);

        // Act
        await _archiveService.ArchiveOldBooks(3, 100);

        // Assert
        _bookRepositoryMock.Verify(
            r => r.UpdateBook(
                bookId,
                It.Is<Book>(b => b.IsArchived && b.Status == BookStatus.Archived)),
            Times.Once);
    }
}