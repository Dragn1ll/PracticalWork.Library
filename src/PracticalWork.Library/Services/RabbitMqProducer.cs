using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.MessageBroker;
using PracticalWork.Library.MessageBroker.Events;
using RabbitMQ.Client;

namespace PracticalWork.Library.Services;

public class RabbitMqProducer : IRabbitMqProducer
{
    private readonly IConnectionFactory _factory;

    public RabbitMqProducer(IOptions<RabbitMqOptions> options)
    {
        _factory = new ConnectionFactory
        {
            HostName = options.Value.HostName,
            UserName = options.Value.UserName,
            Password = options.Value.Password,
            VirtualHost = options.Value.VirtualHost
        };
    }

    public async Task PublishEventAsync(BaseLibraryEvent libraryEvent, CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        
        await channel.QueueDeclareAsync(queue: libraryEvent.EventType, durable: false, exclusive: false, 
            autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        
        var json = JsonSerializer.Serialize(libraryEvent);
        var body = Encoding.UTF8.GetBytes(json);
        
        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: libraryEvent.EventType, body: body, 
            cancellationToken: cancellationToken);
    }
}