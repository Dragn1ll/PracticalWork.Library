using System.Text;
using System.Text.Json;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Services;

public class ReportGenService : IReportGenService
{
    private readonly IReportRepository _reportRepository;
    private readonly IActivityLogRepository _logRepository;
    private readonly IMinIoService _storage;
    private readonly IRedisService _cache;

    public ReportGenService(IReportRepository reportRepository, IActivityLogRepository logRepository,
        IMinIoService storage, IRedisService cache)
    {
        _reportRepository = reportRepository;
        _logRepository = logRepository;
        _storage = storage;
        _cache = cache;
    }

    public async Task GenerateReport(Guid reportId, DateOnly? periodFrom, DateOnly? periodTo, EventType eventType)
    {
        var report = await _reportRepository.GetReportById(reportId);
        var logs = await _logRepository.GetAllActivityLogs(periodFrom, periodTo, eventType);

        try
        {
            var stream = GetStream(logs);
            var fileName = $"{DateTime.UtcNow.Year}/{DateTime.UtcNow.Month}/{reportId}.csv";
            
            await _storage.UploadFileAsync(fileName, stream, "text/csv");
            
            report.MarkAsGenerated(fileName);
            await _reportRepository.UpdateReport(reportId, report);
            
            await _cache.RemoveAsync("reports:list");
        }
        catch (Exception)
        {
            report.Status = ReportStatus.Error;
            await _reportRepository.UpdateReport(reportId,report);
            await _cache.RemoveAsync("reports:list");
            throw;
        }
    }

    private MemoryStream GetStream(IEnumerable<ActivityLog> logs)
    {
        var stringBuilder = new StringBuilder();
        
        stringBuilder.AppendLine("EventType;EventDate;Metadata");
        foreach (var log in logs)
        {
            stringBuilder.AppendLine($"{GetEventTypeName(log.EventType)};{log.EventDate};{JsonSerializer.Serialize(log,
                log.GetType())}");
        }
        
        return new MemoryStream(Encoding.UTF8.GetBytes(stringBuilder.ToString()));
    }

    private string GetEventTypeName(EventType eventType)
    {
        return eventType switch
        {
            EventType.BookCreated => "book.created",
            EventType.BookArchived => "book.archived",
            EventType.BookBorrowed => "book.borrowed",
            EventType.BookReturned => "book.returned",
            EventType.ReaderCreated => "reader.created",
            EventType.ReaderClosed => "reader.closed",
            _ => "???"
        };
    }
}