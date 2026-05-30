using Microsoft.Extensions.Logging;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.Models;
using RazorLight;

namespace PracticalWork.Library.Services;

/// <inheritdoc cref="IEmailTemplateService"/>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly RazorLightEngine _engine;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(ILogger<EmailTemplateService> logger)
    {
        _logger = logger;
        _engine = new RazorLightEngineBuilder()
            .UseEmbeddedResourcesProject(typeof(EmailTemplateService).Assembly, "PracticalWork.Library.resources")
            .Build();
    }

    /// <inheritdoc cref="IEmailTemplateService.RenderReturnReminderAsync"/>
    public async Task<string> RenderReturnReminderAsync(ReturnReminderModel model)
    {
        try
        {
            return await _engine.CompileRenderAsync("ReturnReminder.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка рендеринга шаблона ReturnReminder");
            throw;
        }
    }

    /// <inheritdoc cref="IEmailTemplateService.RenderWeeklyReportAsync"/>
    public async Task<string> RenderWeeklyReportAsync(WeeklyReportModel model)
    {
        try
        {
            return await _engine.CompileRenderAsync("WeeklyReport.cshtml", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка рендеринга шаблона WeeklyReport");
            throw;
        }
    }
}