using Microsoft.EntityFrameworkCore;
using TrackingService.Contracts;
using TrackingService.Data;
using TrackingService.Messaging;
using TrackingService.Models;

var builder = WebApplication.CreateBuilder(args);

var connString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=tracking_db;Username=user;Password=password";

builder.Services.AddDbContext<TrackingDbContext>(opt => opt.UseNpgsql(connString));

var mqHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var mqUser = builder.Configuration["RabbitMq:User"] ?? "guest";
var mqPass = builder.Configuration["RabbitMq:Password"] ?? "guest";

builder.Services.AddSingleton<IEventPublisher>(_ =>
    RabbitMqEventPublisher.CreateAsync(mqHost, mqUser, mqPass).GetAwaiter().GetResult());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
    db.Database.EnsureCreated();
}

// POST /shipments
app.MapPost("/shipments", async (
    CreateShipmentRequest req,
    TrackingDbContext db,
    IEventPublisher publisher,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Sender) ||
        string.IsNullOrWhiteSpace(req.Receiver) ||
        string.IsNullOrWhiteSpace(req.Location))
    {
        return Results.BadRequest(new { message = "Sender, Receiver và Location là bắt buộc." });
    }

    var now = DateTime.UtcNow;
    var trackingCode = $"TRK-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

    var shipment = new Shipment
    {
        TrackingCode = trackingCode,
        Sender = req.Sender.Trim(),
        Receiver = req.Receiver.Trim(),
        Location = req.Location.Trim(),
        Status = ShipmentStatus.Created,
        Timestamp = now
    };

    var initialEvent = new ShipmentEvent
    {
        TrackingCode = trackingCode,
        Status = ShipmentStatus.Created,
        Location = req.Location.Trim(),
        Note = "Khởi tạo bưu gửi",
        Timestamp = now
    };

    db.Shipments.Add(shipment);
    db.ShipmentEvents.Add(initialEvent);
    await db.SaveChangesAsync(ct);

    await publisher.PublishAsync(new ShipmentStatusChangedEvent(
        shipment.TrackingCode,
        shipment.Sender,
        shipment.Receiver,
        ShipmentStatus.Created,
        shipment.Status,
        shipment.Location,
        initialEvent.Note,
        now
    ), ct);

    return Results.Created($"/shipments/{trackingCode}", new ShipmentDto(
        shipment.TrackingCode,
        shipment.Sender,
        shipment.Receiver,
        shipment.Location,
        shipment.Status,
        shipment.Timestamp
    ));
});

// GET /shipments/{trackingCode}
app.MapGet("/shipments/{trackingCode}", async (
    string trackingCode,
    TrackingDbContext db,
    CancellationToken ct) =>
{
    var shipment = await db.Shipments
        .AsNoTracking()
        .FirstOrDefaultAsync(s => s.TrackingCode == trackingCode, ct);

    if (shipment is null)
        return Results.NotFound(new { message = "Không tìm thấy bưu gửi", trackingCode });

    return Results.Ok(new ShipmentDto(
        shipment.TrackingCode,
        shipment.Sender,
        shipment.Receiver,
        shipment.Location,
        shipment.Status,
        shipment.Timestamp
    ));
});

// POST /shipments/{trackingCode}/events
app.MapPost("/shipments/{trackingCode}/events", async (
    string trackingCode,
    CreateShipmentEventRequest req,
    TrackingDbContext db,
    IEventPublisher publisher,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Location))
        return Results.BadRequest(new { message = "Location là bắt buộc." });

    var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.TrackingCode == trackingCode, ct);
    if (shipment is null)
        return Results.NotFound(new { message = "Không tìm thấy bưu gửi", trackingCode });

    var previousStatus = shipment.Status;
    var now = DateTime.UtcNow;

    var evt = new ShipmentEvent
    {
        TrackingCode = trackingCode,
        Status = req.Status,
        Location = req.Location.Trim(),
        Note = req.Note?.Trim(),
        Timestamp = now
    };

    shipment.Status = req.Status;
    shipment.Location = req.Location.Trim();
    shipment.Timestamp = now;

    db.ShipmentEvents.Add(evt);
    await db.SaveChangesAsync(ct);

    if (previousStatus != req.Status)
    {
        await publisher.PublishAsync(new ShipmentStatusChangedEvent(
            shipment.TrackingCode,
            shipment.Sender,
            shipment.Receiver,
            previousStatus,
            shipment.Status,
            shipment.Location,
            evt.Note,
            now
        ), ct);
    }

    return Results.Ok(new ShipmentDto(
        shipment.TrackingCode,
        shipment.Sender,
        shipment.Receiver,
        shipment.Location,
        shipment.Status,
        shipment.Timestamp
    ));
});

// GET /shipments/{trackingCode}/history
app.MapGet("/shipments/{trackingCode}/history", async (
    string trackingCode,
    TrackingDbContext db,
    CancellationToken ct) =>
{
    var shipment = await db.Shipments
        .AsNoTracking()
        .Include(s => s.History)
        .FirstOrDefaultAsync(s => s.TrackingCode == trackingCode, ct);

    if (shipment is null)
        return Results.NotFound(new { message = "Không tìm thấy bưu gửi", trackingCode });

    var events = shipment.History
        .OrderBy(e => e.Timestamp)
        .Select(e => new ShipmentEventDto(e.Id, e.TrackingCode, e.Status, e.Location, e.Note, e.Timestamp))
        .ToList();

    return Results.Ok(new ShipmentHistoryDto(
        shipment.TrackingCode,
        shipment.Sender,
        shipment.Receiver,
        shipment.Location,
        shipment.Status,
        shipment.Timestamp,
        events
    ));
});

app.Run();