using PracticalWork.Library.MessageBroker.Events;

namespace PracticalWork.Library.Abstractions.Services;

/// <summary>
/// Producer для отправки события в очередь RabbitMQ
/// </summary>
public interface IRabbitMqProducer
{
    /// <summary>
    /// Отправить событие в очередь
    /// </summary>
    /// <param name="libraryEvent">Событие</param>
    /// <param name="cancellationToken">Токен прекращения работы</param>
    Task PublishEventAsync<TEvent>(TEvent libraryEvent, CancellationToken cancellationToken = default) 
        where TEvent : BaseLibraryEvent;
}