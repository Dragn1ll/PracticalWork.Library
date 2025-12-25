using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Abstractions.Storage.Repositories;

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
    /// <param name="startDate">Начальная дата логов активности</param>
    /// <param name="endDate">Конечная дата логов активности</param>
    /// <param name="eventType">Тип события</param>
    /// <param name="page">Номер страницы</param>
    /// <param name="pageSize">Размер страницы</param>
    Task<IEnumerable<ActivityLog>> GetAllActivityLogs(DateOnly? startDate, DateOnly? endDate, EventType eventType,
        int page = 1, int pageSize = 20);
}