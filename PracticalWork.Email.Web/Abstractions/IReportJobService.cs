using PracticalWork.Email.Web.Models;

namespace PracticalWork.Email.Web.Abstractions;

/// <summary>
/// Сервис для работы с еженедельными отчетами
/// </summary>
public interface IReportJobService
{
    /// <summary>
    /// Генерация еженедельного отчета за указанный период
    /// </summary>
    Task<GeneratedReport> GenerateWeeklyReport(DateOnly startDate, DateOnly endDate);
}