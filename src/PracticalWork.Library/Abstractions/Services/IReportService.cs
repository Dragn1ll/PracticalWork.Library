using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Library.Abstractions.Services;

public interface IReportService
{
    /// <summary>
    /// Получить все логи активности
    /// </summary>
    /// <param name="dateFrom">Дата начала периода активности</param>
    /// <param name="dateTo">Дата конца периода активности</param>
    /// <param name="eventType">Тип события</param>
    /// <param name="page">Номер страницы</param>
    /// <param name="pageSize">Размер страницы</param>
    /// <returns>Список логов активностей</returns>
    Task<IEnumerable<ActivityLog>> GetAllActivityLogs(DateOnly? dateFrom, DateOnly? dateTo, EventType eventType,
        int page, int pageSize);
    
    /// <summary>
    /// Сгенерировать отчёт
    /// </summary>
    /// <param name="name">Название отчёта</param>
    /// <param name="periodFrom">Дата начала периода отчёта</param>
    /// <param name="periodTo">Дата конца периода отчёта</param>
    /// <param name="eventType">Тип события</param>
    Task GenerateReport(string name, DateOnly periodFrom, DateOnly periodTo, EventType eventType);
    
    /// <summary>
    /// Получить список сгенерированных отчётов
    /// </summary>
    /// <returns>Список отчётов</returns>
    Task<IEnumerable<Report>> GetGeneratedReports();
    
    /// <summary>
    /// Получить ссылку на файл отчёта
    /// </summary>
    /// <param name="reportName">Название отчёта</param>
    /// <returns>Ссылка на отчёт</returns>
    Task<string> GetReportFileUrl(string reportName);
}