using Hangfire;
using Microsoft.Extensions.Options;
using PracticalWork.Email.Web.Configuration;
using PracticalWork.Email.Web.Jobs;

namespace PracticalWork.Email.Web.Hangfire;

/// <summary>
/// Расширения для настройки Hangfire и регистрации recurring jobs
/// </summary>
public static class HangfireExtensions
{
    /// <summary>
    /// Регистрация recurring jobs на основе конфигурации
    /// </summary>
    public static IServiceProvider AddRecurringJobs(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var jobSettings = scope.ServiceProvider
            .GetRequiredService<IOptions<JobSettings>>().Value;

        // Напоминания о возврате (ежедневно)
        if (jobSettings.Jobs.TryGetValue("ReturnReminder", out var reminderConfig))
        {
            RecurringJob.AddOrUpdate<ReturnReminderJob>(
                "return-reminder",
                job => job.ExecuteAsync(CancellationToken.None),
                reminderConfig.CronExpression,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });
        }

        // Еженедельный отчет (каждый понедельник)
        if (jobSettings.Jobs.TryGetValue("WeeklyReport", out var reportConfig))
        {
            RecurringJob.AddOrUpdate<WeeklyReportJob>(
                "weekly-report",
                job => job.ExecuteAsync(CancellationToken.None),
                reportConfig.CronExpression,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });
        }

        // Архивация старых книг (1-е число месяца)
        if (jobSettings.Jobs.TryGetValue("ArchiveOldBooks", out var archiveConfig))
        {
            RecurringJob.AddOrUpdate<ArchiveOldBooksJob>(
                "archive-old-books",
                job => job.ExecuteAsync(CancellationToken.None),
                archiveConfig.CronExpression,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });
        }

        return serviceProvider;
    }
}