using System.Text.Json;
using Microsoft.Extensions.Options;
using PracticalWork.Library.MessageBroker;
using PracticalWork.Library.MessageBroker.Events;
using PracticalWork.Reports.Abstractions.Storage.Repositories;
using PracticalWork.Reports.Enums;
using PracticalWork.Reports.Models;
using PracticalWork.Reports.Worker.Abstractions;
using RabbitMQ.Client;

namespace PracticalWork.Reports.Worker.Consumers;

public class ActivityLogConsumer<T> : RabbitMqConsumer<T> where T : BaseLibraryEvent
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    
    public ActivityLogConsumer(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConsumer<T>> logger, 
        IServiceScopeFactory serviceScopeFactory) : base(options, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ProcessMessageAsync(T? messageObject)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityLogRepository>();

        if (messageObject != null)
        {
            var log = new ActivityLog
            {
                EventDate = messageObject.OccurredOn,
                EventType = GetEventType(messageObject.EventType),
                Metadata = JsonSerializer.Serialize(messageObject, messageObject.GetType())
            };
            
            var props = log.GetType().GetProperties();
            foreach (var prop in props)
            {
                if (prop.Name == "BookId")
                {
                    log.ExternalBookId = prop.GetValue(prop.Name) as Guid?;
                }

                if (prop.Name == "ReaderId")
                {
                    log.ExternalReaderId = prop.GetValue(prop.Name) as Guid?;
                }
            }

            await repository.AddActivityLog(log);
        }
        else
        {
            Logger.LogError("Пришло пустое сообщение лога события");
        }
    }

    private EventType GetEventType(string eventType)
    {
        return eventType switch
        {
            "book.created" => EventType.BookCreated,
            "book.archived" => EventType.BookArchived,
            "book.borrowed" => EventType.BookBorrowed,
            "book.returned" => EventType.BookReturned,
            "reader.created" => EventType.ReaderCreated,
            "reader.closed" => EventType.ReaderClosed,
            _ => EventType.Default
        };
    }
}