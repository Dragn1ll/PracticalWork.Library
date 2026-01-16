using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PracticalWork.Library.MessageBroker;
using PracticalWork.Library.MessageBroker.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PracticalWork.Reports.Worker.Abstractions;

public abstract class RabbitMqConsumer<T> : IRabbitMqConsumer where T : BaseEvent
{
    private readonly Lazy<Task<IConnection>> _connectionTask;
    protected readonly ILogger<RabbitMqConsumer<T>> Logger;
    
    private IChannel? _channel;
    private string? _consumerTag;

    protected RabbitMqConsumer(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConsumer<T>> logger)
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
        Logger = logger;
    }

    public async Task StartAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionTask.Value;
        _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            await DequeueMessageAsync(ea, queueName);
        };
        
        _consumerTag = await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: true,   
            consumer: consumer, cancellationToken: cancellationToken);
            
        Logger.LogInformation("Начато потребление очереди: {QueueName}", queueName);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_consumerTag) && _channel is not null && _channel.IsOpen)
        {
            await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
            Logger.LogInformation("Потребление остановлено");
        }
    }
    
    private async Task DequeueMessageAsync(BasicDeliverEventArgs ea, string queueName)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var properties = ea.BasicProperties;
            
        Logger.LogInformation(
            "Получено сообщение: Queue={Queue}, MessageId={Id}, DeliveryTag={Tag}",
            queueName, properties.MessageId, ea.DeliveryTag);

        var messageObject = JsonSerializer.Deserialize<T>(message);
        if (_channel is null)
        {
            throw new ArgumentNullException();
        }
        await ProcessMessageAsync(messageObject);
        Logger.LogInformation("Сообщение обработано успешно");
    }
    
    protected abstract Task ProcessMessageAsync(T? messageObject);
}