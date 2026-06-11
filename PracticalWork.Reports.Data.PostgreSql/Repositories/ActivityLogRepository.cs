using Microsoft.EntityFrameworkCore;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;
using PracticalWork.Reports.Data.PostgreSql.Entities;

namespace PracticalWork.Reports.Data.PostgreSql.Repositories;

public class ActivityLogRepository : IActivityLogRepository
{
    private readonly ReportDbContext _context;

    public ActivityLogRepository(ReportDbContext context)
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

    public async Task<IEnumerable<ActivityLog>> GetAllActivityLogs(
        DateOnly? startDate,
        DateOnly? endDate,
        EventType eventType,
        int page,
        int pageSize)
    {
        return (await _context.ActivityLogs.AsNoTracking()
            .Where(al =>
                (startDate == null || DateOnly.FromDateTime(al.EventDate) >= startDate) &&
                (endDate == null || DateOnly.FromDateTime(al.EventDate) <= endDate) &&
                (eventType == EventType.Default || al.EventType == eventType))
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
    
    public async Task<ActivityLogStatisticDto> GetStatisticByPeriod(DateTime startDate, DateTime endDate)
    {
        var logs = await _context.ActivityLogs
            .AsNoTracking()
            .Where(a => a.EventDate >= startDate && a.EventDate <= endDate)
            .Select(a => new { a.EventType, a.ExternalBookId, a.EventDate })
            .ToListAsync();

        var borrowedBookIds = logs
            .Where(a => a.EventType == EventType.BookBorrowed)
            .Select(a => a.ExternalBookId)
            .ToHashSet();

        var returnedBookIds = logs
            .Where(a => a.EventType == EventType.BookReturned)
            .Select(a => a.ExternalBookId)
            .ToHashSet();

        var overdue = borrowedBookIds.Count(id => !returnedBookIds.Contains(id));

        return new ActivityLogStatisticDto
        {
            NewBooksCount = logs.Count(a => a.EventType == EventType.BookCreated),
            NewReadersCount = logs.Count(a => a.EventType == EventType.ReaderCreated),
            BorrowedBooksCount = logs.Count(a => a.EventType == EventType.BookBorrowed),
            ReturnedBooksCount = logs.Count(a => a.EventType == EventType.BookReturned),
            OverdueBooksCount = overdue
        };
    }
}