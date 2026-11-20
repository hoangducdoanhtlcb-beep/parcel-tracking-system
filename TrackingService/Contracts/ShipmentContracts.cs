using TrackingService.Models;

namespace TrackingService.Contracts;

public record CreateShipmentRequest(
    string Sender,
    string Receiver,
    string Location
);

public record CreateShipmentEventRequest(
    ShipmentStatus Status,
    string Location,
    string? Note = null
);

public record ShipmentDto(
    string TrackingCode,
    string Sender,
    string Receiver,
    string Location,
    ShipmentStatus Status,
    DateTime Timestamp
);

public record ShipmentEventDto(
    Guid Id,
    string TrackingCode,
    ShipmentStatus Status,
    string Location,
    string? Note,
    DateTime Timestamp
);

public record ShipmentHistoryDto(
    string TrackingCode,
    string Sender,
    string Receiver,
    string Location,
    ShipmentStatus Status,
    DateTime Timestamp,
    List<ShipmentEventDto> History
);

public record ShipmentStatusChangedEvent(
    string TrackingCode,
    string Sender,
    string Receiver,
    ShipmentStatus PreviousStatus,
    ShipmentStatus Status,
    string Location,
    string? Note,
    DateTime Timestamp
);