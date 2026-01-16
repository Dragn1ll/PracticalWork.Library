using System.Text.Json;
using Microsoft.Extensions.Options;
using PracticalWork.Library.Abstractions.Storage.Repositories;
using PracticalWork.Library.MessageBroker;
using PracticalWork.Library.MessageBroker.Events;
using PracticalWork.Library.MessageBroker.Events.Book;
using PracticalWork.Library.MessageBroker.Events.Reader;
using PracticalWork.Library.Models;
using PracticalWork.Library.SharedKernel.Enums;
using PracticalWork.Reports.Worker.Abstractions;

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

            switch (messageObject)
            {
                case BookCreatedEvent bookCreatedEvent:
                    log.ExternalBookId = bookCreatedEvent.BookId;
                    break;
                case BookArchivedEvent bookArchivedEvent:
                    log.ExternalBookId = bookArchivedEvent.BookId;
                    break;
                case ReaderCreatedEvent readerCreatedEvent:
                    log.ExternalReaderId = readerCreatedEvent.ReaderId;
                    break;
                case ReaderClosedEvent readerClosedEvent:
                    log.ExternalReaderId = readerClosedEvent.ReaderId;
                    break;
                case BookBorrowedEvent bookBorrowedEvent:
                    log.ExternalBookId = bookBorrowedEvent.BookId;
                    log.ExternalReaderId = bookBorrowedEvent.ReaderId;
                    break;
                case BookReturnedEvent bookReturnedEvent:
                    log.ExternalBookId = bookReturnedEvent.BookId;
                    log.ExternalReaderId = bookReturnedEvent.ReaderId;
                    break;
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