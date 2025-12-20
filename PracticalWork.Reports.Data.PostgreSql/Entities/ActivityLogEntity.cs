using PracticalWork.Reports.Abstractions.Storage.Entity;

namespace PracticalWork.Reports.Data.PostgreSql.Entities;

/// <summary>
/// Лог активности
/// </summary>
public class ActivityLogEntity : EntityBase
{
    /// <summary>Внешний ключ на Book (опционально)</summary>
    public Guid? ExternalBookId { get; set; }
    
    /// <summary>Внешний ключ на Reader (опционально)</summary>
    public Guid? ExternalReaderId { get; set; }
    
    /// <summary>Тип события</summary>
    public int EventType { get; set; }
    
    /// <summary>Дата события</summary>
    public DateTime EventDate { get; set; }
    
    /// <summary>Дополнительная информация</summary>
    public string Metadata { get; set; } = "{}";
}