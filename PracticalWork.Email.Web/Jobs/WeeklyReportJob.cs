using Microsoft.Extensions.Options;
using PracticalWork.Email.Web.Models;
using PracticalWork.Library.Abstractions.Jobs;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Models;
using PracticalWork.Library.Settings;

namespace PracticalWork.Email.Web.Jobs;

/// <summary>
/// Фоновая задача: еженедельный отчет для администрации
/// </summary>
public class WeeklyReportJob : ILibraryJob
{
    public string JobName => "WeeklyReport";
    public string Description => "Еженедельный отчет для администрации";

    private readonly IReportJobService _reportJobService;
    private readonly ILogger<WeeklyReportJob> _logger;

    public WeeklyReportJob(
        IReportJobService reportJobService,
        ILogger<WeeklyReportJob> logger)
    {
        _reportJobService = reportJobService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Начало выполнения задачи: {JobName}", JobName);

        await _reportJobService.GenerateWeeklyReport();
    }
}