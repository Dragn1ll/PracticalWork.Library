namespace PracticalWork.Library.MessageBroker.Events.Report;

/// <summary>
/// Событие на просьбу создания отчёта
/// </summary>
/// <param name="PeriodFrom">Начало периода отчета</param>
/// <param name="PeriodTo">Конец периода отчета</param>
/// <param name="EventTypeName">Название типа событий</param>
public record CreateReportEvent(
    DateOnly PeriodFrom, 
    DateOnly PeriodTo, 
    string EventTypeName)
    : BaseEvent(Guid.NewGuid(), "report.create", "reports-service");