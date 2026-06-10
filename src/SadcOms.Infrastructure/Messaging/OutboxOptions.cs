namespace SadcOms.Infrastructure.Messaging;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often the dispatcher scans for unpublished messages.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum messages claimed per scan.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Attempts after which a message is considered poison and skipped (left for inspection).</summary>
    public int MaxAttempts { get; set; } = 10;
}
