using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.Dto.Output;
using PracticalWork.Library.Models;
using PracticalWork.Library.Settings;

namespace PracticalWork.Library.Services;

public class NotificationService : INotificationService
{
    private readonly TimeProvider _timeProvider;
    private readonly ILibraryRepository _libraryRepository;
    private readonly EmailTemplateSettings _templateSettings;
    private readonly INotificationLogRepository _notificationLogRepository;
    private readonly IEmailTemplateService _templateService;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(TimeProvider timeProvider, ILibraryRepository libraryRepository, 
        IOptions<EmailTemplateSettings> templateSettings, INotificationLogRepository notificationLogRepository, 
        IEmailTemplateService templateService, IEmailService emailService,
        ILogger<NotificationService> logger)
    {
        _timeProvider = timeProvider;
        _libraryRepository = libraryRepository;
        _templateSettings = templateSettings.Value;
        _notificationLogRepository = notificationLogRepository;
        _templateService = templateService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ReturnReminderResult> NotifyReadersReturnBooks()
    {
        var targetDueDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime
                .AddDays(_templateSettings.ReturnReminder.DaysBeforeDueDate));
        
        var stopWatch = Stopwatch.StartNew();
        
        var borrowedBooksInfo = await _libraryRepository.GetBorrowedIssuedBooksInfo(targetDueDate);
        var notifyResult = await NotifyReadersReturnBooks(borrowedBooksInfo);
        
        stopWatch.Stop();
        notifyResult.ExecutionTime = stopWatch.Elapsed;
        
        return notifyResult;
    }

    private async Task<ReturnReminderResult> NotifyReadersReturnBooks(
        IList<BorrowedIssuedBookInfoDto> borrowedBooksInfo)
    {
        var returnReminderResult = new ReturnReminderResult();
        
        foreach (var borrowedBookInfo in borrowedBooksInfo)
        {
            if (await CanSkipBorrowedBookInfo(borrowedBookInfo))
            {
                returnReminderResult.SkippedCount++;
                continue;
            }

            var subject = _templateSettings.ReturnReminder.SubjectTemplate
                .Replace("{BookTitle}", borrowedBookInfo.BookTitle);
            
            try
            {
                var messageBody = await CreateMessageBody(borrowedBookInfo);
                
                var sendResult = await SendMessage(borrowedBookInfo, subject, messageBody);

                if (sendResult.IsSuccess)
                {
                    returnReminderResult.SentCount++;
                }
                else
                {
                    returnReminderResult.FailedCount++;
                }
            }
            catch (Exception exception)
            {
                returnReminderResult.FailedCount++;
                
                _logger.LogError(exception, "Ошибка обработки напоминания для выдачи {BorrowId}",
                    borrowedBookInfo.BorrowId);

                await _notificationLogRepository.AddNotificationLog(new NotificationLog
                {
                    BorrowId = borrowedBookInfo.BorrowId,
                    NotificationType = "ReturnReminder",
                    RecipientEmail = borrowedBookInfo.ReaderEmail,
                    Subject = subject,
                    IsSent = false,
                    ErrorMessage = exception.Message
                });
            }
        }
        
        return returnReminderResult;
    }

    private async Task<bool> CanSkipBorrowedBookInfo(BorrowedIssuedBookInfoDto borrowedBookInfo)
    {
        if (string.IsNullOrWhiteSpace(borrowedBookInfo.ReaderEmail))
        {
            _logger.LogWarning("Пропуск: у читателя {ReaderName} (ID: {ReaderId}) нет email", 
                borrowedBookInfo.ReaderFullName, borrowedBookInfo.ReaderId);
            
            return true;
        }

        var wasSent = await _notificationLogRepository.WasReminderSentRecently(borrowedBookInfo.BorrowId,
            _templateSettings.ReturnReminder.IntervalInHours);
        
        return wasSent;
    }

    private async Task<string> CreateMessageBody(BorrowedIssuedBookInfoDto borrowedBookInfo)
    {
        var daysRemaining = borrowedBookInfo.BorrowDueDate.DayNumber -
                            DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime).DayNumber;
        
        var model = new ReturnReminderModel
        {
            ReaderName = borrowedBookInfo.ReaderFullName,
            BookTitle = borrowedBookInfo.BookTitle,
            BookAuthors = string.Join(", ", borrowedBookInfo.BookAuthors),
            DueDate = borrowedBookInfo.BorrowDueDate.ToString(_templateSettings.DateFormat),
            DaysRemaining = daysRemaining,
            LibraryName = _templateSettings.LibraryName,
            LibraryAddress = _templateSettings.LibraryAddress,
            LibraryPhone = _templateSettings.LibraryPhone,
            WorkingHours = _templateSettings.WorkingHours
        };

        return await _templateService.RenderReturnReminderAsync(model);
    }

    private async Task<EmailSendResult> SendMessage(BorrowedIssuedBookInfoDto borrowedBookInfo, string subject, string body)
    {
        var sendResult = await _emailService.SendAsync(new EmailMessage
        {
            EmailTo = borrowedBookInfo.ReaderEmail,
            Subject = subject,
            Body = body,
            IsHtml = true
        });

        await _notificationLogRepository.AddNotificationLog(new NotificationLog
        {
            BorrowId = borrowedBookInfo.BorrowId,
            NotificationType = "ReturnReminder",
            RecipientEmail = borrowedBookInfo.ReaderEmail,
            Subject = subject,
            IsSent = sendResult.IsSuccess,
            ErrorMessage = sendResult.Message
        });

        return sendResult;
    }
}