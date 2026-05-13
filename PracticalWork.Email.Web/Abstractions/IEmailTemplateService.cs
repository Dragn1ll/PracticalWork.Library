using PracticalWork.Email.Web.Models;

namespace PracticalWork.Email.Web.Abstractions;

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