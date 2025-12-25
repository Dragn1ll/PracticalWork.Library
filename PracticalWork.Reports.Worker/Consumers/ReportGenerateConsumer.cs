using Microsoft.Extensions.Options;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.MessageBroker;
using PracticalWork.Library.MessageBroker.Events.Report;
using PracticalWork.Library.SharedKernel.Enums;
using PracticalWork.Reports.Worker.Abstractions;

namespace PracticalWork.Reports.Worker.Consumers;

public class ReportGenerateConsumer : RabbitMqConsumer<CreateReportEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    public ReportGenerateConsumer(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConsumer<CreateReportEvent>> logger, 
        IServiceScopeFactory scopeFactory) : base(options, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ProcessMessageAsync(CreateReportEvent? messageObject)
    {
        var scope = _scopeFactory.CreateScope();
        var genService = scope.ServiceProvider.GetRequiredService<IReportGenService>();

        if (messageObject != null)
        {
            await genService.GenerateReport(messageObject.ReportId, messageObject.PeriodFrom, messageObject.PeriodTo, 
                (EventType)messageObject.EventTypeId);
        }
    }
}