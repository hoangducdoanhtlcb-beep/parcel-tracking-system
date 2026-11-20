namespace TrackingService.Models;

public class Shipment
{
    public string TrackingCode { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Created;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ICollection<ShipmentEvent> History { get; set; } = new List<ShipmentEvent>();
}