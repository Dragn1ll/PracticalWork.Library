using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.MessageBroker.Events.Report;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Services;

public class ReportService : IReportService
{
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IRabbitMqProducer _producer;
    private readonly IRedisService _cache;
    private readonly IMinIoService _storage;
    private const string CacheKey = "reports:list";
    private const string BucketName = "reports";

    public ReportService(IActivityLogRepository activityLogRepository, IReportRepository reportRepository, 
        IRabbitMqProducer producer, IRedisService cache, IMinIoService storage)
    {
        _activityLogRepository = activityLogRepository;
        _reportRepository = reportRepository;
        _producer = producer;
        _cache = cache;
        _storage = storage;
    }

    public async Task<IEnumerable<ActivityLog>> GetAllActivityLogs(DateOnly? dateFrom, DateOnly? dateTo, 
        EventType eventType, int page, int pageSize)
    {
        try
        {
            return await _activityLogRepository.GetAllActivityLogs(dateFrom, dateTo, eventType, page, pageSize);
        }
        catch (Exception ex) when (ex is not ClientErrorException)
        {
            throw new ReportServiceException("Не удалось получить записи логов активности", ex);
        }
    }

    public async Task<Report> CreateReport(string name, DateOnly periodFrom, DateOnly periodTo, EventType eventType)
    {
        if (periodTo < periodFrom)
        {
            throw new ClientErrorException("Дата начала периода отчёта не может быть позже даты окончания.");
        }

        var report = new Report
        {
            Name = name,
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
            Status = ReportStatus.InProgress
        };

        try
        {
            var reportId = await _reportRepository.CreateReport(report);
            
            await _producer.PublishEventAsync(new CreateReportEvent(reportId, periodFrom, periodTo, (int)eventType));
            await _cache.RemoveAsync(CacheKey);
            
            return report;
        }
        catch (Exception ex) when (ex is not ClientErrorException)
        {
            throw new ReportServiceException("Не удалось начать генерацию отчёта", ex);
        }
    }

    public async Task<IEnumerable<Report>> GetGeneratedReports()
    {
        try
        {
            var cache = await _cache.GetAsync<IEnumerable<Report>>(CacheKey);

            if (cache == null)
            {
                var reports = (await _reportRepository.GetGeneratedReports()).ToList();
                
                await _cache.SetAsync(CacheKey, reports, new TimeSpan(24, 0, 0));
                
                return reports;
            }
            
            return cache;
        }
        catch (Exception ex) when (ex is not ClientErrorException)
        {
            throw new ReportServiceException("Не удалось получить список готовых отчётов", ex);
        }
    }

    public async Task<string> GetReportFileUrl(string reportName)
    {
        try
        {
            var report = await _reportRepository.GetReportByName(reportName);
            
            var link = await _storage.GetFileUrlAsync(report.FilePath, bucketName: BucketName);
            
            return link;
        }
        catch (Exception ex) when (ex is not ClientErrorException)
        {
            throw new ReportServiceException("Не удалось получить ссылку на файл отчёта", ex);
        }
    }
}