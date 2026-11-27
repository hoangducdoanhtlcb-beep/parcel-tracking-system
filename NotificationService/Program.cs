using NotificationService.Services;
using NotificationService.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<NotificationStore>();
builder.Services.AddHostedService<ShipmentEventConsumer>();

var app = builder.Build();

app.MapGet("/notifications", (NotificationStore store) => Results.Ok(store.GetAll()));

app.MapGet("/notifications/{trackingCode}", (string trackingCode, NotificationStore store) =>
    Results.Ok(store.GetByTrackingCode(trackingCode)));

app.Run();