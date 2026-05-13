using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PracticalWork.Email.Web.Abstractions;
using PracticalWork.Email.Web.Configuration;
using PracticalWork.Email.Web.Models;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.SharedKernel.Enums;
using PracticalWork.Reports.Data.PostgreSql;

namespace PracticalWork.Email.Web.Services;

/// <inheritdoc cref="IReportJobService"/>
public class ReportJobService : IReportJobService
{
    private readonly ReportDbContext _reportDbContext;
    private readonly IMinIoService _minioService;
    private readonly EmailTemplateSettings _templateSettings;
    private readonly ILogger<ReportJobService> _logger;

    public ReportJobService(
        ReportDbContext reportDbContext,
        IMinIoService minioService,
        IOptions<EmailTemplateSettings> templateSettings,
        ILogger<ReportJobService> logger)
    {
        _reportDbContext = reportDbContext;
        _minioService = minioService;
        _templateSettings = templateSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc cref="IReportJobService.GenerateWeeklyReport"/>
    public async Task<GeneratedReport> GenerateWeeklyReport(DateOnly startDate, DateOnly endDate)
    {
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);

        var newBooksCount = await _reportDbContext.ActivityLogs
            .AsNoTracking()
            .CountAsync(a => a.EventType == EventType.BookCreated 
                && a.EventDate >= startDateTime 
                && a.EventDate <= endDateTime);

        var newReadersCount = await _reportDbContext.ActivityLogs
            .AsNoTracking()
            .CountAsync(a => a.EventType == EventType.ReaderCreated 
                && a.EventDate >= startDateTime 
                && a.EventDate <= endDateTime);

        var borrowedBooksCount = await _reportDbContext.ActivityLogs
            .AsNoTracking()
            .CountAsync(a => a.EventType == EventType.BookBorrowed 
                && a.EventDate >= startDateTime 
                && a.EventDate <= endDateTime);

        var returnedBooksCount = await _reportDbContext.ActivityLogs
            .AsNoTracking()
            .CountAsync(a => a.EventType == EventType.BookReturned 
                && a.EventDate >= startDateTime 
                && a.EventDate <= endDateTime);

        var overdueCount = await _reportDbContext.ActivityLogs
            .AsNoTracking()
            .Where(a => a.EventType == EventType.BookBorrowed 
                && a.EventDate <= endDateTime)
            .GroupBy(a => a.ExternalBookId)
            .Select(g => new { BookId = g.Key, BorrowDate = g.Max(x => x.EventDate) })
            .CountAsync(b => !_reportDbContext.ActivityLogs
                .Any(a => a.EventType == EventType.BookReturned 
                    && a.ExternalBookId == b.BookId 
                    && a.EventDate >= b.BorrowDate));

        var fileName = $"report_{endDate:yyyy-MM-dd}.csv";
        var csvContent = GenerateCsvContent(startDate, endDate, 
            newBooksCount, newReadersCount, borrowedBooksCount, returnedBooksCount, overdueCount);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        await _minioService.UploadFileAsync(fileName, stream, "text/csv");

        var downloadUrl = await _minioService.GetFileUrlAsync(fileName, 1440, "library-reports");

        _logger.LogInformation("Еженедельный отчет сгенерирован: {FileName}. Книг: {Books}, Читателей: {Readers}, Выдано: {Borrowed}, Возвращено: {Returned}, Просрочено: {Overdue}",
            fileName, newBooksCount, newReadersCount, borrowedBooksCount, returnedBooksCount, overdueCount);

        return new GeneratedReport
        {
            FileName = fileName,
            DownloadUrl = downloadUrl,
            PeriodFrom = startDate,
            PeriodTo = endDate,
            TotalNewBooks = newBooksCount,
            TotalNewReaders = newReadersCount,
            TotalBorrowed = borrowedBooksCount,
            TotalReturned = returnedBooksCount,
            TotalOverdue = overdueCount
        };
    }

    private static string GenerateCsvContent(DateOnly startDate, DateOnly endDate, 
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
}