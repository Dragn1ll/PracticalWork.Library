using PracticalWork.Library.Abstractions.Storage.Entity;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Reports.Data.PostgreSql.Entities;

/// <summary>
/// Отчет
/// </summary>
public class ReportEntity : EntityBase
{
    /// <summary>Название отчета</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Путь к файлу в MinIO</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Дата генерации</summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>Начало периода отчета</summary>
    public DateOnly PeriodFrom { get; set; }
    
    /// <summary>Конец периода отчета</summary>
    public DateOnly PeriodTo { get; set; }

    /// <summary>Статус</summary>
    public ReportStatus Status { get; set; }
}