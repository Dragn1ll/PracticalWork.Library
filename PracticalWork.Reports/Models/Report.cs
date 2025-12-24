namespace PracticalWork.Reports.Models;

/// <summary>
/// Отчет
/// </summary>
public class Report
{
    /// <summary>Название отчета</summary>
    public string Name { get; set; } = null!;

    /// <summary>Путь к файлу в MinIO</summary>
    public string FilePath { get; set; } = null!;

    /// <summary>Дата генерации</summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>Начало периода отчета</summary>
    public DateOnly PeriodFrom { get; set; }
    
    /// <summary>Конец периода отчета</summary>
    public DateOnly PeriodTo { get; set; }

    /// <summary>Статус</summary>
    public int Status { get; set; }
}