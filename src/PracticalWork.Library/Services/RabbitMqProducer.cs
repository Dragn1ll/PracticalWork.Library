using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PracticalWork.Library.Abstractions.Services;
using PracticalWork.Library.MessageBroker;
using PracticalWork.Library.MessageBroker.Events;
using RabbitMQ.Client;

namespace PracticalWork.Library.Services;

public class RabbitMqProducer : IRabbitMqProducer, IAsyncDisposable
{
    private readonly Lazy<Task<IConnection>> _connectionTask;

    public RabbitMqProducer(IOptions<RabbitMqOptions> options)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.Value.HostName,
            Port = options.Value.Port,
            UserName = options.Value.UserName,
            Password = options.Value.Password,
            VirtualHost = options.Value.VirtualHost,

            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        _connectionTask = new Lazy<Task<IConnection>>(() => factory.CreateConnectionAsync());
    }

    public async Task PublishEventAsync<TEvent>(TEvent libraryEvent, CancellationToken cancellationToken = default) 
        where TEvent : BaseEvent
    {
        var connection = await _connectionTask.Value;

        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: libraryEvent.EventType,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(libraryEvent));

        var props = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
            MessageId = Guid.NewGuid().ToString("N")
        };

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: libraryEvent.EventType,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (!_connectionTask.IsValueCreated) return;

        var conn = await _connectionTask.Value;
        await conn.CloseAsync();
        await conn.DisposeAsync();
    }
}