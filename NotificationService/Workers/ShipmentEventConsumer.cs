using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NotificationService.Services;

namespace NotificationService.Workers;

public class ShipmentEventConsumer : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly NotificationStore _store;
    private readonly ILogger<ShipmentEventConsumer> _logger;
    private const string ExchangeName = "shipment.events";
    private const string QueueName = "notification.shipment-events";

    public ShipmentEventConsumer(IConfiguration config, NotificationStore store, ILogger<ShipmentEventConsumer> logger)
    {
        _config = config;
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMq:Host"] ?? "localhost",
            UserName = _config["RabbitMq:User"] ?? "guest",
            Password = _config["RabbitMq:Password"] ?? "guest"
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(QueueName, ExchangeName, string.Empty, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var msg = new NotificationMessage(
                    TrackingCode: root.GetProperty("TrackingCode").GetString() ?? "",
                    Sender: root.GetProperty("Sender").GetString() ?? "",
                    Receiver: root.GetProperty("Receiver").GetString() ?? "",
                    PreviousStatus: root.GetProperty("PreviousStatus").GetString() ?? "",
                    Status: root.GetProperty("Status").GetString() ?? "",
                    Location: root.GetProperty("Location").GetString() ?? "",
                    Note: root.TryGetProperty("Note", out var n) ? n.GetString() : null,
                    Timestamp: root.TryGetProperty("Timestamp", out var t) && t.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow,
                    NotifiedAt: DateTime.UtcNow
                );

                _store.Add(msg);
                _logger.LogInformation("Thông báo bưu gửi {Code}: {Prev} -> {Curr} tại {Loc}",
                    msg.TrackingCode, msg.PreviousStatus, msg.Status, msg.Location);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xử lý event từ RabbitMQ");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}