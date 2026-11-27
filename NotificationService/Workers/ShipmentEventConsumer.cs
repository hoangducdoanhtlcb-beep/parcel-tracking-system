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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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
                            TrackingCode: ReadStringOrNumber(root, "TrackingCode"),
                            Sender: ReadStringOrNumber(root, "Sender"),
                            Receiver: ReadStringOrNumber(root, "Receiver"),
                            PreviousStatus: ReadStatus(root, "PreviousStatus"),
                            Status: ReadStatus(root, "Status"),
                            Location: ReadStringOrNumber(root, "Location"),
                            Note: ReadNullableString(root, "Note"),
                            Timestamp: root.TryGetProperty("Timestamp", out var t) && t.ValueKind == JsonValueKind.String && t.TryGetDateTime(out var dt)
                                ? dt
                                : DateTime.UtcNow,
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
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, stoppingToken);
                _logger.LogInformation("Đã kết nối RabbitMQ thành công và lắng nghe queue {Queue}", QueueName);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chưa kết nối được hoặc mất kết nối RabbitMQ. Thử lại sau 5 giây...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static string ReadStringOrNumber(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var el))
            return "";

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? "",
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.Null => "",
            _ => el.GetRawText()
        };
    }

    private static string? ReadNullableString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var el) || el.ValueKind == JsonValueKind.Null)
            return null;

        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
    }

    private static string ReadStatus(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var el))
            return "";

        if (el.ValueKind == JsonValueKind.String)
            return el.GetString() ?? "";

        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var code))
        {
            return code switch
            {
                0 => "Created",
                1 => "InTransit",
                2 => "AtHub",
                3 => "Delivered",
                4 => "Failed",
                _ => code.ToString()
            };
        }

        return el.GetRawText();
    }
}