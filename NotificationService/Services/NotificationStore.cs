using System.Collections.Concurrent;

namespace NotificationService.Services;

public record NotificationMessage(
    string TrackingCode,
    string Sender,
    string Receiver,
    string PreviousStatus,
    string Status,
    string Location,
    string? Note,
    DateTime Timestamp,
    DateTime NotifiedAt
);

public class NotificationStore
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<NotificationMessage>> _byCode = new();
    private readonly ConcurrentQueue<NotificationMessage> _all = new();

    public void Add(NotificationMessage msg)
    {
        var q = _byCode.GetOrAdd(msg.TrackingCode, _ => new ConcurrentQueue<NotificationMessage>());
        q.Enqueue(msg);

        _all.Enqueue(msg);
        while (_all.Count > 200 && _all.TryDequeue(out _)) { }
    }

    public IReadOnlyList<NotificationMessage> GetByTrackingCode(string trackingCode)
        => _byCode.TryGetValue(trackingCode, out var q) ? q.ToArray() : Array.Empty<NotificationMessage>();

    public IReadOnlyList<NotificationMessage> GetAll()
        => _all.ToArray();
}