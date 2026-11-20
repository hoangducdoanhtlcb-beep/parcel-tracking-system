namespace TrackingService.Models;

public class ShipmentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TrackingCode { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Shipment? Shipment { get; set; }
}