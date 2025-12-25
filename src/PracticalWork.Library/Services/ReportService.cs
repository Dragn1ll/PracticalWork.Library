using PracticalWork.Library.Abstractions.Storage.Repositories;
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

    public async Task<IEnumerable<ActivityLog>> GetAllActivities(DateOnly? date, EventType eventType, int? page,
        int? pageSize)
    {
        try
        {
            return await _activityLogRepository.GetAllActivityLogs(date, eventType, page ?? 1, pageSize ?? 20);
        }
        catch (Exception)
        {
            throw;
        }
    }
    
    
}