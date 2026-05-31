using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PracticalWork.Email.Web.Models;
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
        _logger.LogInformation("Генерация отчета за период: {Start} - {End}", period.StartDate, period.EndDate);

        var report = await GenerateWeeklyReport(period.StartDate, period.EndDate);
        
        await SendWeeklyReportToAdmins(period, report);
    }
    
    private async Task<GeneratedReport> GenerateWeeklyReport(DateOnly startDate, DateOnly endDate)
    {
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);
        
        var activityLogStatistic = await _activityLogRepository.GetStatisticByPeriod(startDateTime, endDateTime);
        var report = await CreateWeeklyReportFile(startDate, endDate, activityLogStatistic);
        
        return report;
    }

    private string GenerateCsvContent(DateOnly startDate, DateOnly endDate, 
        int newBooks, int newReaders, int borrowed, int returned, int overdue)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Показатель;Значение");
        sb.AppendLine($"Период;{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");
        sb.AppendLine($"Новые книги;{newBooks}");
        sb.AppendLine($"Новые читатели;{newReaders}");
        sb.AppendLine($"Выдано книг;{borrowed}");
        sb.AppendLine($"Возвращено книг;{returned}");
        sb.AppendLine($"Просроченные выдачи;{overdue}");
        return sb.ToString();
    }

    private async Task<GeneratedReport> CreateWeeklyReportFile(DateOnly startDate, DateOnly endDate,
        ActivityLogStatisticDto activityLogStatistic)
    {
        var fileName = $"report_{endDate:yyyy-MM-dd}.csv";
        var csvContent = GenerateCsvContent(startDate, endDate, 
            activityLogStatistic.NewBooksCount, 
            activityLogStatistic.NewReadersCount, 
            activityLogStatistic.BorrowedBooksCount, 
            activityLogStatistic.ReturnedBooksCount, 
            activityLogStatistic.OverdueBooksCount);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        await _minioService.UploadFileAsync(fileName, stream, "text/csv");

        var downloadUrl = await _minioService.GetFileUrlAsync(fileName,
            _templateSettings.WeeklyReport.IntervalInMinutes, "library-reports");

        _logger.LogInformation("Еженедельный отчет сгенерирован: {FileName}. " +
                               "Книг: {Books}, " +
                               "Читателей: {Readers}, " +
                               "Выдано: {Borrowed}, " +
                               "Возвращено: {Returned}, " +
                               "Просрочено: {Overdue}",
            fileName, 
            activityLogStatistic.NewBooksCount, 
            activityLogStatistic.NewReadersCount, 
            activityLogStatistic.BorrowedBooksCount, 
            activityLogStatistic.ReturnedBooksCount, 
            activityLogStatistic.OverdueBooksCount);
        
        return new GeneratedReport
        {
            FileName = fileName,
            DownloadUrl = downloadUrl,
            PeriodFrom = startDate,
            PeriodTo = endDate,
            TotalNewBooks = activityLogStatistic.NewBooksCount,
            TotalNewReaders = activityLogStatistic.NewReadersCount,
            TotalBorrowed = activityLogStatistic.BorrowedBooksCount,
            TotalReturned = activityLogStatistic.ReturnedBooksCount,
            TotalOverdue = activityLogStatistic.OverdueBooksCount
        };
    }

    private (DateOnly StartDate, DateOnly EndDate) GetPeriodWeeklyReport()
    {
        var today = _timeProvider.GetUtcNow().DateTime;
        var daysSinceLastMonday = ((int)today.DayOfWeek + MondayOffset) % DaysInWeek;
        var previousMonday = today.Date.AddDays(-daysSinceLastMonday - DaysInWeek);
        var previousSunday = previousMonday.AddDays(MondayOffset);
        
        return (DateOnly.FromDateTime(previousMonday), DateOnly.FromDateTime(previousSunday));
    }

    private async Task SendWeeklyReportToAdmins((DateOnly StartDate, DateOnly EndDate) period,
        GeneratedReport report)
    {
        var messageBody = await GenerateWeeklyReportMessageBody(period, report);

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
                _logger.LogError("Ошибка отправки отчета администратору {Email}: {Error}", 
                    adminEmail, result.Message);
            }
        }

        _logger.LogInformation("Еженедельный отчет отправлен {Count} администраторам",
            _templateSettings.WeeklyReport.AdminEmails.Length);
    }

    private async Task<string> GenerateWeeklyReportMessageBody((DateOnly StartDate, DateOnly EndDate) period,
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
            GeneratedAt = DateTime.UtcNow.ToString(_templateSettings.DateTimeFormat),
            LibraryName = _templateSettings.LibraryName
        };

        return await _templateService.RenderWeeklyReportAsync(model);
    }
}