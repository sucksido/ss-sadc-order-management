namespace SadcOms.Contracts;

/// <summary>
/// Base for all integration events placed on the bus. Carries the metadata a consumer
/// needs for idempotency (<see cref="EventId"/>), tracing (<see cref="CorrelationId"/>)
/// and schema evolution (<see cref="Version"/>).
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Flows from the originating HTTP request so logs/traces can be stitched together end to end.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Contract version. Consumers branch on this to stay backward compatible.</summary>
    public int Version { get; init; } = 1;
}
