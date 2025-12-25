using Microsoft.EntityFrameworkCore;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;
using PracticalWork.Reports.Data.PostgreSql.Entities;

namespace PracticalWork.Reports.Data.PostgreSql.Repositories;

public class ActivityLogRepository : IActivityLogRepository
{
    private readonly AppDbContext _context;

    public ActivityLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddActivityLog(ActivityLog activityLog)
    {
        var entity = new ActivityLogEntity
        {
            ExternalBookId = activityLog.ExternalBookId,
            ExternalReaderId = activityLog.ExternalReaderId,
            EventType = activityLog.EventType,
            EventDate = activityLog.EventDate,
            Metadata = activityLog.Metadata
        };
        
        _context.ActivityLogs.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ActivityLog>> GetAllActivityLogs(DateOnly? startDate, DateOnly? endDate, 
        EventType eventType, int page = 1, int pageSize = 20)
    {
        return (await _context.ActivityLogs.AsNoTracking()
            .Where(al => (DateOnly.FromDateTime(al.EventDate) >= startDate || startDate == null)
                         && (DateOnly.FromDateTime(al.EventDate) <= endDate || endDate == null)
                         && (al.EventType == eventType || eventType == EventType.Default))
            .OrderBy(al => al.EventDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync())
            .Select(al => new ActivityLog
            {
                ExternalBookId = al.ExternalBookId,
                ExternalReaderId = al.ExternalReaderId,
                EventType = al.EventType,
                EventDate = al.EventDate,
                Metadata = al.Metadata
            });
    }
}