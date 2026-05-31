using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Models;
using PracticalWork.Library.Services;
using PracticalWork.Library.Settings;

namespace PracticalWork.Email.Tests;

public class ReportJobServiceTests
{
    private readonly Mock<IActivityLogRepository> _activityLogRepositoryMock;
    private readonly Mock<IMinIoService> _minioServiceMock;
    private readonly Mock<IEmailTemplateService> _templateServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<ReportJobService>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IReportJobService _reportJobService;
 
    private static readonly DateTimeOffset FakeNow =
        new(2025, 6, 16, 10, 0, 0, TimeSpan.Zero);
 
    private readonly EmailTemplateSettings _templateSettings = new()
    {
        LibraryName = "Тестовая библиотека",
        DateFormat = "dd.MM.yyyy",
        DateTimeFormat = "dd.MM.yyyy HH:mm",
        WeeklyReport = new WeeklyReportTemplate
        {
            SubjectTemplate = "Еженедельный отчет библиотеки за период {StartDate} - {EndDate}",
            AdminEmails = ["admin@test.com", "admin2@test.com"],
            ReportRetentionDays = 90,
            IntervalInMinutes = 1440
        }
    };
 
    public ReportJobServiceTests()
    {
        _activityLogRepositoryMock = new Mock<IActivityLogRepository>();
        _minioServiceMock = new Mock<IMinIoService>();
        _templateServiceMock = new Mock<IEmailTemplateService>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<ReportJobService>>();
        _timeProvider = new FakeTimeProvider(FakeNow);
 
        _reportJobService = new ReportJobService(
            _activityLogRepositoryMock.Object,
            _minioServiceMock.Object,
            Options.Create(_templateSettings),
            _timeProvider,
            _templateServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object
        );
    }
 
    private void SetupDefaultMocks(ActivityLogStatisticDto? statistic = null)
    {
        statistic ??= new ActivityLogStatisticDto
        {
            NewBooksCount = 5,
            NewReadersCount = 3,
            BorrowedBooksCount = 10,
            ReturnedBooksCount = 8,
            OverdueBooksCount = 2
        };
 
        _activityLogRepositoryMock
            .Setup(r => r.GetStatisticByPeriod(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(statistic);
 
        _minioServiceMock
            .Setup(m => m.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
 
        _minioServiceMock
            .Setup(m => m.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync("https://minio.example.com/report.csv");
 
        _templateServiceMock
            .Setup(t => t.RenderWeeklyReportAsync(It.IsAny<WeeklyReportModel>()))
            .ReturnsAsync("<html>weekly report</html>");
 
        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .ReturnsAsync(new EmailSendResult { IsSuccess = true });
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_WhenNoAdmins_LogsWarningAndReturnsEarly()
    {
        // Arrange
        var settingsNoAdmin = new EmailTemplateSettings
        {
            WeeklyReport = new WeeklyReportTemplate { AdminEmails = [] }
        };
 
        var service = new ReportJobService(
            _activityLogRepositoryMock.Object,
            _minioServiceMock.Object,
            Options.Create(settingsNoAdmin),
            _timeProvider,
            _templateServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
 
        // Act
        await service.GenerateWeeklyReport();
 
        // Assert
        _activityLogRepositoryMock.Verify(
            r => r.GetStatisticByPeriod(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Never);
 
        _emailServiceMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>()), Times.Never);
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_QueriesStatisticForPreviousWeek()
    {
        // Arrange
        var expectedStart = new DateTime(2025, 6, 9, 0, 0, 0);
        var expectedEnd = new DateTime(2025, 6, 15, 23, 59, 59, 999).AddTicks(9999);
 
        SetupDefaultMocks();
 
        // Act
        await _reportJobService.GenerateWeeklyReport();
 
        // Assert
        _activityLogRepositoryMock.Verify(
            r => r.GetStatisticByPeriod(
                It.Is<DateTime>(d => d.Date == expectedStart.Date),
                It.Is<DateTime>(d => d.Date == expectedEnd.Date)),
            Times.Once);
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_UploadsFileToCsvToMinio()
    {
        // Arrange
        SetupDefaultMocks();
 
        // Act
        await _reportJobService.GenerateWeeklyReport();
 
        // Assert
        _minioServiceMock.Verify(
            m => m.UploadFileAsync(
                It.Is<string>(name => name.StartsWith("report_") && name.EndsWith(".csv")),
                It.IsAny<Stream>(),
                "text/csv"),
            Times.Once);
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_SendsEmailToEachAdmin()
    {
        // Arrange
        SetupDefaultMocks();
 
        // Act
        await _reportJobService.GenerateWeeklyReport();
 
        // Assert
        _emailServiceMock.Verify(
            e => e.SendAsync(It.IsAny<EmailMessage>()),
            Times.Exactly(2));
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_SendsEmailWithCorrectAdminAddresses()
    {
        // Arrange
        SetupDefaultMocks();
 
        var sentEmails = new List<string>();
        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .Callback<EmailMessage>(m => sentEmails.Add(m.EmailTo))
            .ReturnsAsync(new EmailSendResult { IsSuccess = true });
 
        // Act
        await _reportJobService.GenerateWeeklyReport();
 
        // Assert
        Assert.Contains("admin@test.com", sentEmails);
        Assert.Contains("admin2@test.com", sentEmails);
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_SubjectContainsPeriodDates()
    {
        // Arrange
        SetupDefaultMocks();
 
        EmailMessage? capturedMessage = null;
        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .Callback<EmailMessage>(m => capturedMessage = m)
            .ReturnsAsync(new EmailSendResult { IsSuccess = true });
 
        // Act
        await _reportJobService.GenerateWeeklyReport();
 
        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains("09.06.2025", capturedMessage.Subject);
        Assert.Contains("15.06.2025", capturedMessage.Subject);
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_RendersTemplateWithCorrectStatistics()
    {
        // Arrange
        var statistic = new ActivityLogStatisticDto
        {
            NewBooksCount = 7,
            NewReadersCount = 4,
            BorrowedBooksCount = 15,
            ReturnedBooksCount = 12,
            OverdueBooksCount = 3
        };
 
        SetupDefaultMocks(statistic);
 
        WeeklyReportModel? capturedModel = null;
        _templateServiceMock
            .Setup(t => t.RenderWeeklyReportAsync(It.IsAny<WeeklyReportModel>()))
            .Callback<WeeklyReportModel>(m => capturedModel = m)
            .ReturnsAsync("<html>report</html>");
 
        // Act
        await _reportJobService.GenerateWeeklyReport();
 
        // Assert
        Assert.NotNull(capturedModel);
        Assert.Equal(7, capturedModel.NewBooksCount);
        Assert.Equal(4, capturedModel.NewReadersCount);
        Assert.Equal(15, capturedModel.BorrowedBooksCount);
        Assert.Equal(12, capturedModel.ReturnedBooksCount);
        Assert.Equal(3, capturedModel.OverdueCount);
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_RenderedBodySentAsHtml()
    {
        // Arrange
        const string expectedBody = "<html>отчет</html>";
        SetupDefaultMocks();
 
        _templateServiceMock
            .Setup(t => t.RenderWeeklyReportAsync(It.IsAny<WeeklyReportModel>()))
            .ReturnsAsync(expectedBody);
 
        EmailMessage? capturedMessage = null;
        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .Callback<EmailMessage>(m => capturedMessage = m)
            .ReturnsAsync(new EmailSendResult { IsSuccess = true });
 
        // Act
        await _reportJobService.GenerateWeeklyReport();
 
        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Equal(expectedBody, capturedMessage.Body);
        Assert.True(capturedMessage.IsHtml);
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_WhenSendEmailFails_ContinuesToNextAdmin()
    {
        // Arrange
        SetupDefaultMocks();
 
        var sentCount = 0;
        _emailServiceMock
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>()))
            .ReturnsAsync(() =>
            {
                sentCount++;
                return sentCount == 1
                    ? new EmailSendResult { IsSuccess = false, Message = "SMTP недоступен" }
                    : new EmailSendResult { IsSuccess = true };
            });
 
        // Act
        await _reportJobService.GenerateWeeklyReport();
 
        // Assert
        _emailServiceMock.Verify(e => e.SendAsync(It.IsAny<EmailMessage>()), Times.Exactly(2));
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_ReportFileNameMatchesEndDate()
    {
        // Arrange
        SetupDefaultMocks();
 
        string? capturedFileName = null;
        _minioServiceMock
            .Setup(m => m.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<string, Stream, string>((name, _, _) => capturedFileName = name)
            .Returns(Task.CompletedTask);
 
        // Act
        await _reportJobService.GenerateWeeklyReport();
 
        // Assert
        Assert.Equal("report_2025-06-15.csv", capturedFileName);
    }
 
    [Fact]
    public async Task GenerateWeeklyReport_TemplateModelContainsDownloadUrl()
    {
        // Arrange
        const string expectedUrl = "https://minio.example.com/reports/report.csv";
        SetupDefaultMocks();
 
        _minioServiceMock
            .Setup(m => m.GetFileUrlAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(expectedUrl);
 
        WeeklyReportModel? capturedModel = null;
        _templateServiceMock
            .Setup(t => t.RenderWeeklyReportAsync(It.IsAny<WeeklyReportModel>()))
            .Callback<WeeklyReportModel>(m => capturedModel = m)
            .ReturnsAsync("<html>report</html>");
 
        // Act
        await _reportJobService.GenerateWeeklyReport();
 
        // Assert
        Assert.NotNull(capturedModel);
        Assert.Equal(expectedUrl, capturedModel.ReportDownloadUrl);
    }
}