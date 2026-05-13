using Microsoft.Extensions.Options;
using PracticalWork.Email.Web.Abstractions;
using PracticalWork.Email.Web.Configuration;
using PracticalWork.Email.Web.Models;

namespace PracticalWork.Email.Web.Jobs;

/// <summary>
/// Фоновая задача: еженедельный отчет для администрации
/// </summary>
public class WeeklyReportJob : ILibraryJob
{
    public string JobName => "WeeklyReport";
    public string Description => "Еженедельный отчет для администрации";

    private readonly IReportJobService _reportJobService;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _templateService;
    private readonly EmailTemplateSettings _templateSettings;
    private readonly ILogger<WeeklyReportJob> _logger;

    public WeeklyReportJob(
        IReportJobService reportJobService,
        IEmailService emailService,
        IEmailTemplateService templateService,
        IOptions<EmailTemplateSettings> templateSettings,
        ILogger<WeeklyReportJob> logger)
    {
        _reportJobService = reportJobService;
        _emailService = emailService;
        _templateService = templateService;
        _templateSettings = templateSettings.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Начало выполнения задачи: {JobName}", JobName);

        var adminEmails = _templateSettings.WeeklyReport.AdminEmails;
        if (adminEmails.Length == 0)
        {
            _logger.LogWarning("Список email администраторов пуст. Отчет не отправлен.");
            return;
        }

        var today = DateTime.UtcNow;
        var daysSinceLastMonday = ((int)today.DayOfWeek + 6) % 7;
        var previousMonday = today.Date.AddDays(-daysSinceLastMonday - 7);
        var previousSunday = previousMonday.AddDays(6);

        var startDate = DateOnly.FromDateTime(previousMonday);
        var endDate = DateOnly.FromDateTime(previousSunday);

        _logger.LogInformation("Генерация отчета за период: {Start} - {End}", startDate, endDate);

        GeneratedReport report;
        try
        {
            report = await _reportJobService.GenerateWeeklyReport(startDate, endDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации отчета");
            return;
        }

        var model = new WeeklyReportModel
        {
            PeriodStart = startDate.ToString("dd.MM.yyyy"),
            PeriodEnd = endDate.ToString("dd.MM.yyyy"),
            NewBooksCount = report.TotalNewBooks,
            NewReadersCount = report.TotalNewReaders,
            BorrowedBooksCount = report.TotalBorrowed,
            ReturnedBooksCount = report.TotalReturned,
            OverdueCount = report.TotalOverdue,
            ReportDownloadUrl = report.DownloadUrl,
            GeneratedAt = DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm"),
            LibraryName = _templateSettings.LibraryName
        };

        var subject = _templateSettings.WeeklyReport.SubjectTemplate
            .Replace("{StartDate}", startDate.ToString("dd.MM.yyyy"))
            .Replace("{EndDate}", endDate.ToString("dd.MM.yyyy"));

        try
        {
            var htmlBody = await _templateService.RenderWeeklyReportAsync(model);
            var textBody = BuildPlainTextReport(model, report.DownloadUrl);

            foreach (var adminEmail in adminEmails)
            {
                var result = await _emailService.SendAsync(new EmailMessage
                {
                    To = adminEmail,
                    Subject = subject,
                    HtmlBody = htmlBody,
                    TextBody = textBody,
                    IsHtml = true
                });

                if (!result.Success)
                {
                    _logger.LogError("Ошибка отправки отчета администратору {Email}: {Error}", 
                        adminEmail, result.ErrorMessage);
                }
            }

            _logger.LogInformation("Еженедельный отчет отправлен {Count} администраторам", adminEmails.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки еженедельного отчета");
        }
    }

    private static string BuildPlainTextReport(WeeklyReportModel model, string downloadUrl)
    {
        return $@"ЕЖЕНЕДЕЛЬНЫЙ ОТЧЕТ БИБЛИОТЕКИ
                =============================
                ПЕРИОД: {model.PeriodStart} - {model.PeriodEnd}

                ОСНОВНАЯ СТАТИСТИКА:
                • Новые книги: {model.NewBooksCount}
                • Новые читатели: {model.NewReadersCount}
                • Выдано книг: {model.BorrowedBooksCount}
                • Возвращено книг: {model.ReturnedBooksCount}
                • Просроченные выдачи: {model.OverdueCount}

                Полный отчет доступен для скачивания по ссылке:
                {downloadUrl}

                Отчет сгенерирован автоматически: {model.GeneratedAt}

                --Это автоматически сгенерированное сообщение.
                Для получения дополнительной информации обратитесь в систему.";
    }
}