namespace PracticalWork.Email.Web.Configuration;

/// <summary>
/// Настройки шаблонов email сообщений
/// </summary>
public class EmailTemplateSettings
{
    public ReturnReminderTemplate ReturnReminder { get; set; } = new();
    public WeeklyReportTemplate WeeklyReport { get; set; } = new();
    public string LibraryName { get; set; } = "Библиотека";
    public string LibraryAddress { get; set; } = "";
    public string LibraryPhone { get; set; } = "";
    public string WorkingHours { get; set; } = "";
}

public class ReturnReminderTemplate
{
    public string SubjectTemplate { get; set; } = "Напоминание о возврате книги: \"{BookTitle}\"";
    public int DaysBeforeDueDate { get; set; } = 3;
}

public class WeeklyReportTemplate
{
    public string SubjectTemplate { get; set; } = "Еженедельный отчет библиотеки за период {StartDate} - {EndDate}";
    public string[] AdminEmails { get; set; } = [];
    public int ReportRetentionDays { get; set; } = 90;
}