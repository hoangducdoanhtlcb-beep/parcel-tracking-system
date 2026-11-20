using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using TrackingService.Contracts;

namespace TrackingService.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(ShipmentStatusChangedEvent evt, CancellationToken ct = default);
}

public class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private const string ExchangeName = "shipment.events";

    private RabbitMqEventPublisher(IConnection connection, IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public static async Task<RabbitMqEventPublisher> CreateAsync(string hostName, string userName, string password)
    {
        var factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password
        };

        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false);

        return new RabbitMqEventPublisher(connection, channel);
    }

    public async Task PublishAsync(ShipmentStatusChangedEvent evt, CancellationToken ct = default)
    {
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));
        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: props,
            body: payload,
            cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}