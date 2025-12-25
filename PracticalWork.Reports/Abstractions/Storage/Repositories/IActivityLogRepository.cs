using PracticalWork.Reports.Enums;
using PracticalWork.Reports.Models;

namespace PracticalWork.Reports.Abstractions.Storage.Repositories;

/// <summary>
/// Репозитория для логов активности
/// </summary>
public interface IActivityLogRepository
{
    /// <summary>
    /// Добавить лог активности
    /// </summary>
    /// <param name="activityLog">Лог активности</param>
    Task AddActivityLog(ActivityLog activityLog);

    /// <summary>
    /// Получение логов активности 
    /// </summary>
    /// <param name="date">Дата лога активности</param>
    /// <param name="eventType">Тип события</param>
    /// <param name="page">Номер страницы</param>
    /// <param name="pageSize">Размер страницы</param>
    Task<IEnumerable<ActivityLog>> GetAllActivityLogs(DateOnly? date, EventType eventType, int page, int pageSize);
}