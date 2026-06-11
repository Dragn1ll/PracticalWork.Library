using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Models;
using PracticalWork.Library.Settings;

namespace PracticalWork.Library.Services;

/// <inheritdoc cref="IReportJobService"/>
public class ReportJobService : IReportJobService
{
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly IMinIoService _minioService;
    private readonly EmailTemplateSettings _templateSettings;
    private readonly TimeProvider _timeProvider;
    private readonly IEmailTemplateService _templateService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ReportJobService> _logger;

    private const int DaysInWeek = 7;
    private const int MondayOffset = 6;
    private const string ReportBucketName = "library-reports";
    private const string ReportContentType = "text/csv";

    public ReportJobService(
        IActivityLogRepository activityLogRepository,
        IMinIoService minioService,
        IOptions<EmailTemplateSettings> templateSettings,
        TimeProvider timeProvider,
        IEmailTemplateService templateService,
        IEmailService emailService,
        ILogger<ReportJobService> logger)
    {
        _activityLogRepository = activityLogRepository;
        _minioService = minioService;
        _templateSettings = templateSettings.Value;
        _timeProvider = timeProvider;
        _templateService = templateService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <inheritdoc cref="IReportJobService.GenerateWeeklyReport"/>
    public async Task GenerateWeeklyReport()
    {
        var adminEmails = _templateSettings.WeeklyReport.AdminEmails;
        if (adminEmails.Length == 0)
        {
            _logger.LogWarning("Список email администраторов пуст. Отчет не отправлен.");
            return;
        }

        var period = GetPeriodWeeklyReport();
        _logger.LogInformation(
            "Генерация отчета за период: {Start} - {End}",
            period.StartDate, period.EndDate);

        var report = await GenerateWeeklyReportAsync(period.StartDate, period.EndDate);

        await SendWeeklyReportToAdminsAsync(period, report);
    }

    private async Task<GeneratedReport> GenerateWeeklyReportAsync(DateOnly startDate, DateOnly endDate)
    {
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);

        var statistic = await _activityLogRepository.GetStatisticByPeriod(startDateTime, endDateTime);

        return await CreateWeeklyReportFileAsync(startDate, endDate, statistic);
    }

    private async Task<GeneratedReport> CreateWeeklyReportFileAsync(
        DateOnly startDate,
        DateOnly endDate,
        ActivityLogStatisticDto statistic)
    {
        var fileName = $"report_{endDate:yyyy-MM-dd}.csv";
        var csvContent = BuildCsvContent(startDate, endDate, statistic);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        await _minioService.UploadFileAsync(fileName, stream, ReportContentType, ReportBucketName);

        var downloadUrl = await _minioService.GetFileUrlAsync(
            fileName,
            _templateSettings.WeeklyReport.IntervalInMinutes,
            ReportBucketName);

        _logger.LogInformation(
            "Еженедельный отчет сгенерирован: {FileName}. " +
            "Книг: {Books}, Читателей: {Readers}, " +
            "Выдано: {Borrowed}, Возвращено: {Returned}, Просрочено: {Overdue}",
            fileName,
            statistic.NewBooksCount,
            statistic.NewReadersCount,
            statistic.BorrowedBooksCount,
            statistic.ReturnedBooksCount,
            statistic.OverdueBooksCount);

        return new GeneratedReport
        {
            FileName = fileName,
            DownloadUrl = downloadUrl,
            PeriodFrom = startDate,
            PeriodTo = endDate,
            TotalNewBooks = statistic.NewBooksCount,
            TotalNewReaders = statistic.NewReadersCount,
            TotalBorrowed = statistic.BorrowedBooksCount,
            TotalReturned = statistic.ReturnedBooksCount,
            TotalOverdue = statistic.OverdueBooksCount
        };
    }

    private static string BuildCsvContent(
        DateOnly startDate,
        DateOnly endDate,
        ActivityLogStatisticDto stat)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Показатель;Значение");
        sb.AppendLine($"Период;{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");
        sb.AppendLine($"Новые книги;{stat.NewBooksCount}");
        sb.AppendLine($"Новые читатели;{stat.NewReadersCount}");
        sb.AppendLine($"Выдано книг;{stat.BorrowedBooksCount}");
        sb.AppendLine($"Возвращено книг;{stat.ReturnedBooksCount}");
        sb.AppendLine($"Просроченные выдачи;{stat.OverdueBooksCount}");
        return sb.ToString();
    }

    private (DateOnly StartDate, DateOnly EndDate) GetPeriodWeeklyReport()
    {
        var today = _timeProvider.GetUtcNow().DateTime;
        var daysSinceLastMonday = ((int)today.DayOfWeek + MondayOffset) % DaysInWeek;
        var previousMonday = today.Date.AddDays(-daysSinceLastMonday - DaysInWeek);
        var previousSunday = previousMonday.AddDays(MondayOffset);

        return (DateOnly.FromDateTime(previousMonday), DateOnly.FromDateTime(previousSunday));
    }

    private async Task SendWeeklyReportToAdminsAsync(
        (DateOnly StartDate, DateOnly EndDate) period,
        GeneratedReport report)
    {
        var messageBody = await BuildWeeklyReportMessageBodyAsync(period, report);

        var subject = _templateSettings.WeeklyReport.SubjectTemplate
            .Replace("{StartDate}", period.StartDate.ToString(_templateSettings.DateFormat))
            .Replace("{EndDate}", period.EndDate.ToString(_templateSettings.DateFormat));

        foreach (var adminEmail in _templateSettings.WeeklyReport.AdminEmails)
        {
            var result = await _emailService.SendAsync(new EmailMessage
            {
                EmailTo = adminEmail,
                Subject = subject,
                Body = messageBody,
                IsHtml = true
            });

            if (!result.IsSuccess)
            {
                _logger.LogError(
                    "Ошибка отправки отчета администратору {Email}: {Error}",
                    adminEmail, result.Message);
            }
        }

        _logger.LogInformation(
            "Еженедельный отчет отправлен {Count} администраторам",
            _templateSettings.WeeklyReport.AdminEmails.Length);
    }

    private async Task<string> BuildWeeklyReportMessageBodyAsync(
        (DateOnly StartDate, DateOnly EndDate) period,
        GeneratedReport report)
    {
        var model = new WeeklyReportModel
        {
            PeriodStart = period.StartDate.ToString(_templateSettings.DateFormat),
            PeriodEnd = period.EndDate.ToString(_templateSettings.DateFormat),
            NewBooksCount = report.TotalNewBooks,
            NewReadersCount = report.TotalNewReaders,
            BorrowedBooksCount = report.TotalBorrowed,
            ReturnedBooksCount = report.TotalReturned,
            OverdueCount = report.TotalOverdue,
            ReportDownloadUrl = report.DownloadUrl,
            GeneratedAt = _timeProvider.GetUtcNow().UtcDateTime.ToString(_templateSettings.DateTimeFormat),
            LibraryName = _templateSettings.LibraryName
        };

        return await _templateService.RenderWeeklyReportAsync(model);
    }
}