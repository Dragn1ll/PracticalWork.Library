using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Models;
using PracticalWork.Library.Services;
using PracticalWork.Library.Settings;

namespace PracticalWork.Email.Tests;

public class NotificationServiceTests
{
    private readonly Mock<ILibraryRepository>             _libraryRepositoryMock;
    private readonly Mock<INotificationLogRepository>     _notificationLogRepositoryMock;
    private readonly Mock<IEmailTemplateService>          _templateServiceMock;
    private readonly Mock<IEmailService>                  _emailServiceMock;
    private readonly INotificationService                 _notificationService;
    private readonly FakeTimeProvider                     _timeProvider;

    private static readonly DateTimeOffset FakeNow =
        new(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly EmailTemplateSettings _templateSettings = new()
    {
        LibraryName    = "Тестовая библиотека",
        LibraryAddress = "ул. Тестовая, 1",
        LibraryPhone   = "+7 000 000-00-00",
        WorkingHours   = "09:00 - 18:00",
        DateFormat     = "dd.MM.yyyy",
        ReturnReminder = new ReturnReminderTemplate
        {
            DaysBeforeDueDate = 3,
            IntervalInHours   = 24,
            SubjectTemplate   = "Напоминание о возврате книги: \"{BookTitle}\""
        }
    };

    public NotificationServiceTests()
    {
        _libraryRepositoryMock         = new Mock<ILibraryRepository>();
        _notificationLogRepositoryMock = new Mock<INotificationLogRepository>();
        _templateServiceMock           = new Mock<IEmailTemplateService>();
        _emailServiceMock              = new Mock<IEmailService>();

        Mock<ILogger<NotificationService>> loggerMock = new();
        _timeProvider = new FakeTimeProvider(FakeNow);

        _notificationService = new NotificationService(
            _timeProvider,
            _libraryRepositoryMock.Object,
            Options.Create(_templateSettings),
            _notificationLogRepositoryMock.Object,
            _templateServiceMock.Object,
            _emailServiceMock.Object,
            loggerMock.Object
        );
    }

    private BorrowedIssuedBookInfoDto CreateBorrowInfo(
        string email       = "reader@test.com",
        string readerName  = "Иван Иванов",
        string bookTitle   = "Война и мир",
        int    dueDaysFromNow = 3)
    {
        return new BorrowedIssuedBookInfoDto
        {
            BorrowId        = Guid.NewGuid(),
            ReaderId        = Guid.NewGuid(),
            ReaderFullName  = readerName,
            ReaderEmail     = email,
            BookTitle       = bookTitle,
            BookAuthors     = new List<string> { "Толстой" },
            BorrowDueDate   = DateOnly.FromDateTime(
                _timeProvider.GetUtcNow().UtcDateTime.AddDays(dueDaysFromNow))
        };
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_WhenNoBorrows_ReturnsZeroCounts()
    {
        // Arrange
        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto>());

        // Act
        var result = await _notificationService.NotifyReadersReturnBooks();

        // Assert
        Assert.Equal(0, result.SentCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_WhenReaderHasNoEmail_SkipsThem()
    {
        // Arrange
        var borrowInfoNoEmail = CreateBorrowInfo(email: "");

        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto> { borrowInfoNoEmail });

        // Act
        var result = await _notificationService.NotifyReadersReturnBooks();

        // Assert
        Assert.Equal(0, result.SentCount);
        Assert.Equal(1, result.SkippedCount);

        _emailServiceMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>()), Times.Never);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_WhenReminderAlreadySentRecently_SkipsBorrow()
    {
        // Arrange
        var borrowInfo = CreateBorrowInfo();

        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto> { borrowInfo });

        _notificationLogRepositoryMock
            .Setup(r => r.WasReminderSentRecently(borrowInfo.BorrowId, 24))
            .ReturnsAsync(true);

        // Act
        var result = await _notificationService.NotifyReadersReturnBooks();

        // Assert
        Assert.Equal(0, result.SentCount);
        Assert.Equal(1, result.SkippedCount);

        _emailServiceMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>()), Times.Never);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_WhenSendSucceeds_IncrementsSentCount()
    {
        // Arrange
        var borrowInfo = CreateBorrowInfo();

        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto> { borrowInfo });

        _notificationLogRepositoryMock
            .Setup(r => r.WasReminderSentRecently(borrowInfo.BorrowId, 24))
            .ReturnsAsync(false);

        _templateServiceMock
            .Setup(t => t.RenderReturnReminderAsync(It.IsAny<ReturnReminderModel>()))
            .ReturnsAsync("<html>test</html>");

        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .ReturnsAsync(new EmailSendResult { IsSuccess = true, Message = "OK" });

        _notificationLogRepositoryMock
            .Setup(r => r.AddNotificationLog(It.IsAny<NotificationLog>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _notificationService.NotifyReadersReturnBooks();

        // Assert
        Assert.Equal(1, result.SentCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_WhenSendFails_IncrementsFailedCount()
    {
        // Arrange
        var borrowInfo = CreateBorrowInfo();

        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto> { borrowInfo });

        _notificationLogRepositoryMock
            .Setup(r => r.WasReminderSentRecently(borrowInfo.BorrowId, 24))
            .ReturnsAsync(false);

        _templateServiceMock
            .Setup(t => t.RenderReturnReminderAsync(It.IsAny<ReturnReminderModel>()))
            .ReturnsAsync("<html>test</html>");

        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .ReturnsAsync(new EmailSendResult { IsSuccess = false, Message = "SMTP error" });

        _notificationLogRepositoryMock
            .Setup(r => r.AddNotificationLog(It.IsAny<NotificationLog>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _notificationService.NotifyReadersReturnBooks();

        // Assert
        Assert.Equal(0, result.SentCount);
        Assert.Equal(1, result.FailedCount);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_WhenTemplateFails_IncrementsFailedCountAndLogsError()
    {
        // Arrange
        var borrowInfo = CreateBorrowInfo();

        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto> { borrowInfo });

        _notificationLogRepositoryMock
            .Setup(r => r.WasReminderSentRecently(borrowInfo.BorrowId, 24))
            .ReturnsAsync(false);

        _templateServiceMock
            .Setup(t => t.RenderReturnReminderAsync(It.IsAny<ReturnReminderModel>()))
            .ThrowsAsync(new InvalidOperationException("Шаблон не найден"));

        _notificationLogRepositoryMock
            .Setup(r => r.AddNotificationLog(It.IsAny<NotificationLog>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _notificationService.NotifyReadersReturnBooks();

        // Assert
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(0, result.SentCount);

        _notificationLogRepositoryMock.Verify(
            r => r.AddNotificationLog(It.Is<NotificationLog>(l => !l.IsSent)),
            Times.Once);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_WhenSendSucceeds_SavesNotificationLogWithIsSentTrue()
    {
        // Arrange
        var borrowInfo = CreateBorrowInfo();

        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto> { borrowInfo });

        _notificationLogRepositoryMock
            .Setup(r => r.WasReminderSentRecently(borrowInfo.BorrowId, 24))
            .ReturnsAsync(false);

        _templateServiceMock
            .Setup(t => t.RenderReturnReminderAsync(It.IsAny<ReturnReminderModel>()))
            .ReturnsAsync("<html>body</html>");

        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .ReturnsAsync(new EmailSendResult { IsSuccess = true });

        _notificationLogRepositoryMock
            .Setup(r => r.AddNotificationLog(It.IsAny<NotificationLog>()))
            .Returns(Task.CompletedTask);

        // Act
        await _notificationService.NotifyReadersReturnBooks();

        // Assert
        _notificationLogRepositoryMock.Verify(
            r => r.AddNotificationLog(It.Is<NotificationLog>(l =>
                l.IsSent &&
                l.BorrowId == borrowInfo.BorrowId &&
                l.RecipientEmail == borrowInfo.ReaderEmail &&
                l.NotificationType == "ReturnReminder")),
            Times.Once);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_SubjectContainsBookTitle()
    {
        // Arrange
        var borrowInfo = CreateBorrowInfo(bookTitle: "Мастер и Маргарита");

        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto> { borrowInfo });

        _notificationLogRepositoryMock
            .Setup(r => r.WasReminderSentRecently(borrowInfo.BorrowId, 24))
            .ReturnsAsync(false);

        _templateServiceMock
            .Setup(t => t.RenderReturnReminderAsync(It.IsAny<ReturnReminderModel>()))
            .ReturnsAsync("<html>body</html>");

        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .ReturnsAsync(new EmailSendResult { IsSuccess = true });

        _notificationLogRepositoryMock
            .Setup(r => r.AddNotificationLog(It.IsAny<NotificationLog>()))
            .Returns(Task.CompletedTask);

        // Act
        await _notificationService.NotifyReadersReturnBooks();

        // Assert
        _emailServiceMock.Verify(
            e => e.SendAsync(It.Is<EmailMessage>(m
                => m.Subject.Contains("Мастер и Маргарита"))),
            Times.Once);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_TargetDueDateCalculatedCorrectly()
    {
        // Arrange
        var expectedTargetDate = new DateOnly(2025, 6, 18);

        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(expectedTargetDate))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto>())
            .Verifiable();

        // Act
        await _notificationService.NotifyReadersReturnBooks();

        // Assert
        _libraryRepositoryMock.Verify(
            r => r.GetBorrowedIssuedBooksInfo(expectedTargetDate),
            Times.Once);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_WhenMultipleBorrows_ProcessesEachIndependently()
    {
        // Arrange
        var borrow1 = CreateBorrowInfo(email: "user1@test.com", bookTitle: "Книга 1");
        var borrow2 = CreateBorrowInfo(email: "",                bookTitle: "Книга 2");
        var borrow3 = CreateBorrowInfo(email: "user3@test.com", bookTitle: "Книга 3");

        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto> { borrow1, borrow2, borrow3 });

        _notificationLogRepositoryMock
            .Setup(r => r.WasReminderSentRecently(It.IsAny<Guid>(), It.IsAny<int>()))
            .ReturnsAsync(false);

        _templateServiceMock
            .Setup(t => t.RenderReturnReminderAsync(It.IsAny<ReturnReminderModel>()))
            .ReturnsAsync("<html>body</html>");

        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .ReturnsAsync(new EmailSendResult { IsSuccess = true });

        _notificationLogRepositoryMock
            .Setup(r => r.AddNotificationLog(It.IsAny<NotificationLog>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _notificationService.NotifyReadersReturnBooks();

        // Assert
        Assert.Equal(2, result.SentCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_ReturnsNonNegativeExecutionTime()
    {
        // Arrange
        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto>());

        // Act
        var result = await _notificationService.NotifyReadersReturnBooks();

        // Assert
        Assert.True(result.ExecutionTime >= TimeSpan.Zero);
    }

    [Fact]
    public async Task NotifyReadersReturnBooks_RendersTemplateWithCorrectModel()
    {
        // Arrange
        var borrowInfo = CreateBorrowInfo(
            readerName: "Пётр Петров",
            bookTitle: "Преступление и наказание");

        ReturnReminderModel capturedModel = null!;

        _libraryRepositoryMock
            .Setup(r => r.GetBorrowedIssuedBooksInfo(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<BorrowedIssuedBookInfoDto> { borrowInfo });

        _notificationLogRepositoryMock
            .Setup(r => r.WasReminderSentRecently(borrowInfo.BorrowId, 24))
            .ReturnsAsync(false);

        _templateServiceMock
            .Setup(t => t.RenderReturnReminderAsync(It.IsAny<ReturnReminderModel>()))
            .Callback<ReturnReminderModel>(m => capturedModel = m)
            .ReturnsAsync("<html>body</html>");

        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .ReturnsAsync(new EmailSendResult { IsSuccess = true });

        _notificationLogRepositoryMock
            .Setup(r => r.AddNotificationLog(It.IsAny<NotificationLog>()))
            .Returns(Task.CompletedTask);

        // Act
        await _notificationService.NotifyReadersReturnBooks();

        // Assert
        Assert.NotNull(capturedModel);
        Assert.Equal("Пётр Петров",               capturedModel.ReaderName);
        Assert.Equal("Преступление и наказание",   capturedModel.BookTitle);
        Assert.Equal(_templateSettings.LibraryName,    capturedModel.LibraryName);
        Assert.Equal(_templateSettings.LibraryAddress, capturedModel.LibraryAddress);
    }
}