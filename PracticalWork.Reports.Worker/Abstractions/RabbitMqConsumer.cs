using System.Text;
using System.Text.Json;
using PracticalWork.Library.MessageBroker.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PracticalWork.Reports.Worker.Abstractions;

public abstract class RabbitMqConsumer<T> : IRabbitMqConsumer where T : BaseEvent
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqConsumer<T>> _logger;
    
    private IChannel? _channel;
    private string? _consumerTag;

    protected RabbitMqConsumer(IConnection connection, ILogger<RabbitMqConsumer<T>> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task StartAsync(string queueName, CancellationToken cancellationToken = default)
    {
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            await DequeueMessageAsync(ea, queueName);
        };
        
        _consumerTag = await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: true,   
            consumer: consumer, cancellationToken: cancellationToken);
            
        _logger.LogInformation("Начато потребление очереди: {QueueName}", queueName);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_consumerTag) && _channel is not null && _channel.IsOpen)
        {
            await _channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
            _logger.LogInformation("Потребление остановлено");
        }
    }
    
    private async Task DequeueMessageAsync(BasicDeliverEventArgs ea, string queueName)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var properties = ea.BasicProperties;
            
        _logger.LogInformation(
            "Получено сообщение: Queue={Queue}, MessageId={Id}, DeliveryTag={Tag}",
            queueName, properties.MessageId, ea.DeliveryTag);

        var messageObject = JsonSerializer.Deserialize<T>(message);
        if (_channel is null)
        {
            throw new ArgumentNullException();
        }
        await ProcessMessageAsync(messageObject);
        _logger.LogInformation("Сообщение обработано успешно");
    }
    
    protected abstract Task ProcessMessageAsync(T? messageObject);
}