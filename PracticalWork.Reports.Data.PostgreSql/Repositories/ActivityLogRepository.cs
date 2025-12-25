using Microsoft.EntityFrameworkCore;
using PracticalWork.Reports.Abstractions.Storage.Repositories;
using PracticalWork.Reports.Data.PostgreSql.Entities;
using PracticalWork.Reports.Enums;
using PracticalWork.Reports.Models;

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

    public async Task<IEnumerable<ActivityLog>> GetAllActivityLogs(DateOnly? date, EventType eventType, int page,
        int pageSize)
    {
        return (await _context.ActivityLogs.AsNoTracking()
            .Where(al => (DateOnly.FromDateTime(al.EventDate) == date || date == null)
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