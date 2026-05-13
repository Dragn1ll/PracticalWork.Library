using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PracticalWork.Email.Web.Abstractions;
using PracticalWork.Email.Web.Configuration;
using PracticalWork.Email.Web.Models;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Data.PostgreSql;
using PracticalWork.Library.SharedKernel.Enums;

namespace PracticalWork.Email.Web.Jobs;

/// <summary>
/// Фоновая задача: автоматические напоминания о возврате книг
/// </summary>
public class ReturnReminderJob : ILibraryJob
{
    public string JobName => "ReturnReminder";
    public string Description => "Автоматические напоминания о возврате книг";

    private readonly AppDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _templateService;
    private readonly INotificationLogRepository _notificationLogRepository;
    private readonly EmailTemplateSettings _templateSettings;
    private readonly ILogger<ReturnReminderJob> _logger;

    public ReturnReminderJob(
        AppDbContext dbContext,
        IEmailService emailService,
        IEmailTemplateService templateService,
        INotificationLogRepository notificationLogRepository,
        IOptions<EmailTemplateSettings> templateSettings,
        ILogger<ReturnReminderJob> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _templateService = templateService;
        _notificationLogRepository = notificationLogRepository;
        _templateSettings = templateSettings.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Начало выполнения задачи: {JobName}", JobName);

        var daysBeforeDue = _templateSettings.ReturnReminder.DaysBeforeDueDate;
        var targetDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysBeforeDue));

        var borrowsDueSoon = await (
            from borrow in _dbContext.BookBorrows
            join book in _dbContext.Books on borrow.BookId equals book.Id
            join reader in _dbContext.Readers on borrow.ReaderId equals reader.Id
            where borrow.Status == BookIssueStatus.Issued 
                  && borrow.DueDate == targetDueDate
            select new { Borrow = borrow, Book = book, Reader = reader }
        ).AsNoTracking().ToListAsync(cancellationToken);

        var sentCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

        foreach (var item in borrowsDueSoon)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(item.Reader.Email))
            {
                skippedCount++;
                _logger.LogWarning("Пропуск: у читателя {ReaderName} (ID: {ReaderId}) нет email", 
                    item.Reader.FullName, item.Reader.Id);
                continue;
            }

            var wasSent = await _notificationLogRepository.WasReminderSentRecently(item.Borrow.Id, 24);
            if (wasSent)
            {
                skippedCount++;
                continue;
            }

            var daysRemaining = item.Borrow.DueDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
            
            var model = new ReturnReminderModel
            {
                ReaderName = item.Reader.FullName,
                BookTitle = item.Book.Title,
                BookAuthors = string.Join(", ", item.Book.Authors),
                DueDate = item.Borrow.DueDate.ToString("dd.MM.yyyy"),
                DaysRemaining = daysRemaining,
                LibraryName = _templateSettings.LibraryName,
                LibraryAddress = _templateSettings.LibraryAddress,
                LibraryPhone = _templateSettings.LibraryPhone,
                WorkingHours = _templateSettings.WorkingHours
            };

            var subject = _templateSettings.ReturnReminder.SubjectTemplate
                .Replace("{BookTitle}", item.Book.Title);

            try
            {
                var htmlBody = await _templateService.RenderReturnReminderAsync(model);
                var textBody = BuildPlainTextReminder(model);

                var result = await _emailService.SendAsync(new EmailMessage
                {
                    To = item.Reader.Email,
                    Subject = subject,
                    HtmlBody = htmlBody,
                    TextBody = textBody,
                    IsHtml = true
                });

                await _notificationLogRepository.AddNotificationLog(new Library.Models.NotificationLog
                {
                    BorrowId = item.Borrow.Id,
                    NotificationType = "ReturnReminder",
                    RecipientEmail = item.Reader.Email,
                    Subject = subject,
                    Status = result.Success ? "Success" : "Error",
                    ErrorMessage = result.ErrorMessage
                });

                if (result.Success)
                    sentCount++;
                else
                    failedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogError(ex, "Ошибка обработки напоминания для выдачи {BorrowId}", item.Borrow.Id);

                await _notificationLogRepository.AddNotificationLog(new Library.Models.NotificationLog
                {
                    BorrowId = item.Borrow.Id,
                    NotificationType = "ReturnReminder",
                    RecipientEmail = item.Reader.Email,
                    Subject = subject,
                    Status = "Error",
                    ErrorMessage = ex.Message
                });
            }
        }

        _logger.LogInformation(
            "Задача {JobName} завершена: отправлено {Sent}, ошибок {Failed}, пропущено {Skipped}",
            JobName, sentCount, failedCount, skippedCount);
    }

    private static string BuildPlainTextReminder(ReturnReminderModel model)
    {
        return $@"Уважаемый(ая) {model.ReaderName}!
                Напоминаем вам о необходимости возврата книги в библиотеку.

                ИНФОРМАЦИЯ О КНИГЕ:
                Название: {model.BookTitle}
                Автор(ы): {model.BookAuthors}
                Срок возврата: {model.DueDate}
                Осталось дней: {model.DaysRemaining}

                Пожалуйста, верните книгу до указанной даты.

                КОНТАКТЫ БИБЛИОТЕКИ:
                • Адрес: {model.LibraryAddress}
                • Телефон: {model.LibraryPhone}
                • Часы работы: {model.WorkingHours}

                С уважением,
                Администрация библиотеки

                --Это письмо было отправлено автоматически.
                Если вы уже вернули книгу, проигнорируйте это сообщение.";
    }
}