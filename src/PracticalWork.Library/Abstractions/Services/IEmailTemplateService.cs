using PracticalWork.Library.Models;

namespace PracticalWork.Library.Abstractions.Services;

/// <summary>
/// Сервис для рендеринга HTML email шаблонов
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Рендеринг шаблона напоминания о возврате книги
    /// </summary>
    Task<string> RenderReturnReminderAsync(ReturnReminderModel model);

    /// <summary>
    /// Рендеринг шаблона еженедельного отчета
    /// </summary>
    Task<string> RenderWeeklyReportAsync(WeeklyReportModel model);
}