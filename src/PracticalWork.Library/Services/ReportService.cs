using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Services;

public class ReportService
{
    private readonly IActivityLogRepository _activityLogRepository;

    public ReportService(IActivityLogRepository activityLogRepository)
    {
        _activityLogRepository = activityLogRepository;
    }

    public async Task<IEnumerable<ActivityLog>> GetAllActivityLogs(DateOnly? dateFrom, DateOnly? dateTo, 
        EventType eventType, int page, int pageSize)
    {
        try
        {
            return await _activityLogRepository.GetAllActivityLogs(dateFrom, dateTo, eventType, page, pageSize);
        }
        catch (Exception ex)
        {
            throw new ReportServiceException("Не удалось получить записи логов активности", ex);
        }
    }
    
    
}