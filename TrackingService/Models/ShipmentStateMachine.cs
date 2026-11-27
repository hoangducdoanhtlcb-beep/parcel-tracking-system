namespace TrackingService.Models;

public static class ShipmentStateMachine
{
    private static readonly Dictionary<ShipmentStatus, ShipmentStatus[]> AllowedTransitions = new()
    {
        [ShipmentStatus.Created]   = [ShipmentStatus.InTransit, ShipmentStatus.AtHub, ShipmentStatus.Failed],
        [ShipmentStatus.InTransit] = [ShipmentStatus.AtHub, ShipmentStatus.Delivered, ShipmentStatus.Failed],
        [ShipmentStatus.AtHub]     = [ShipmentStatus.InTransit, ShipmentStatus.Delivered, ShipmentStatus.Failed],
        [ShipmentStatus.Failed]    = [ShipmentStatus.InTransit, ShipmentStatus.AtHub],
        [ShipmentStatus.Delivered] = [] // Trạng thái kết thúc
    };

    public static bool CanTransition(ShipmentStatus current, ShipmentStatus next)
    {
        if (current == next) return false;
        return AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);
    }

    public static string GetAllowedTransitionsText(ShipmentStatus current)
    {
        if (!AllowedTransitions.TryGetValue(current, out var allowed) || allowed.Length == 0)
            return "Không có (đơn hàng đã hoàn tất)";

        return string.Join(", ", allowed);
    }
}