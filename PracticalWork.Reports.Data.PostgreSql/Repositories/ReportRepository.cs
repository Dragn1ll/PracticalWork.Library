using Microsoft.EntityFrameworkCore;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Exceptions;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;
using PracticalWork.Reports.Data.PostgreSql.Entities;

namespace PracticalWork.Reports.Data.PostgreSql.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly ReportDbContext _context;

    public ReportRepository(ReportDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> CreateReport(Report report)
    {
        if (await _context.Reports.FirstOrDefaultAsync(r => r.Name == report.Name) != null)
        {
            throw new ClientErrorException("Уже существует отчёт с таким названием.");
        }
        
        var entity = new ReportEntity()
        {
            Name = report.Name,
            PeriodFrom = report.PeriodFrom,
            PeriodTo = report.PeriodTo,
            Status = report.Status,
            FilePath = report.FilePath ?? string.Empty,
            GeneratedAt = report.GeneratedAt
        };
        
        _context.Reports.Add(entity);
        await _context.SaveChangesAsync();
        
        return entity.Id;
    }

    public async Task<Report> GetReportById(Guid reportId)
    {
        var entity = await _context.Reports.FindAsync(reportId);
        if (entity == null)
        {
            throw new ClientErrorException("Не существует отчёта с таким идентификатором");
        }
        
        return new Report
        {
            Name = entity.Name,
            PeriodFrom = entity.PeriodFrom,
            PeriodTo = entity.PeriodTo,
            Status = entity.Status,
            FilePath = entity.FilePath,
            GeneratedAt = entity.GeneratedAt
        };
    }

    public async Task<Report> GetReportByName(string reportName)
    {
        var entity = await _context.Reports.FirstOrDefaultAsync(r => r.Name == reportName);
        if (entity == null)
        {
            throw new ClientErrorException("Не существует отчёта с таким названием");
        }
        
        return new Report
        {
            Name = entity.Name,
            PeriodFrom = entity.PeriodFrom,
            PeriodTo = entity.PeriodTo,
            Status = entity.Status,
            FilePath = entity.FilePath,
            GeneratedAt = entity.GeneratedAt
        };
    }

    public async Task<IEnumerable<Report>> GetGeneratedReports()
    {
        return await _context.Reports.Where(r => r.Status == ReportStatus.Generated)
            .Select(r => new Report
            {
                Name = r.Name,
                PeriodFrom = r.PeriodFrom,
                PeriodTo = r.PeriodTo,
                Status = r.Status,
                FilePath = r.FilePath,
                GeneratedAt = r.GeneratedAt
            })
            .ToListAsync();
    }

    public async Task UpdateReport(Guid reportId, Report report)
    {
        var entity = await _context.Reports.FindAsync(reportId);
        if (entity == null)
        {
            throw new ClientErrorException("Не существует отчёта с таким идентификатором");
        }
        
        entity.FilePath = report.FilePath;
        entity.Status = report.Status;
        
        _context.Reports.Update(entity);
        await _context.SaveChangesAsync();
    }
}