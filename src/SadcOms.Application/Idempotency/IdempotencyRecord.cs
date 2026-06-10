namespace SadcOms.Application.Idempotency;

/// <summary>
/// Stores the outcome of a mutating request keyed by its client-supplied Idempotency-Key.
/// A replay of the same key returns the original result instead of re-applying the change,
/// which makes status updates safe under client retries and at-least-once delivery.
/// </summary>
public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
        Key = null!;
        RequestTarget = null!;
    }

    public IdempotencyRecord(string key, string requestTarget, int statusCode, string? responseBody, DateTimeOffset createdAt)
    {
        Key = key;
        RequestTarget = requestTarget;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        CreatedAt = createdAt;
    }

    /// <summary>The client-supplied Idempotency-Key (primary key).</summary>
    public string Key { get; private set; }

    /// <summary>"METHOD path" the key was used against; guards against reusing a key on a different endpoint.</summary>
    public string RequestTarget { get; private set; }

    public int StatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
