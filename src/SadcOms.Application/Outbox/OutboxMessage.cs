namespace SadcOms.Application.Outbox;

/// <summary>
/// A transactional-outbox row. When a use case mutates business state and needs to publish
/// an event, it writes the event here in the <em>same</em> database transaction. A separate
/// dispatcher later reads unprocessed rows and publishes them to the broker. This removes
/// the dual-write problem (committing the order but failing to publish, or vice versa).
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
        Type = null!;
        Payload = null!;
    }

    public OutboxMessage(string type, string payload, string? correlationId, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        Type = type;
        Payload = payload;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Assembly-qualified-ish logical type name used to deserialise on dispatch.</summary>
    public string Type { get; private set; }

    /// <summary>JSON-serialised event body.</summary>
    public string Payload { get; private set; }

    public string? CorrelationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>How many publish attempts have been made; used for backoff and poison detection.</summary>
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    public void MarkProcessed(DateTimeOffset when)
    {
        ProcessedAt = when;
        LastError = null;
    }

    public void RecordFailure(string error)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}
