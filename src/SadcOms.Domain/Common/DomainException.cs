namespace SadcOms.Domain.Common;

/// <summary>
/// Base type for errors that represent a violation of a business rule. These are
/// translated to HTTP 400/409 at the API boundary rather than surfacing as 500s.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

/// <summary>Raised when an order status change is not allowed by the lifecycle state machine.</summary>
public sealed class InvalidStatusTransitionException : DomainException
{
    public InvalidStatusTransitionException(string from, string to)
        : base($"Cannot transition an order from '{from}' to '{to}'.")
    {
        From = from;
        To = to;
    }

    public string From { get; }
    public string To { get; }
}
