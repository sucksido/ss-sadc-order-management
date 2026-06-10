namespace SadcOms.Contracts;

/// <summary>
/// Published when an order is created. The downstream worker consumes this to simulate
/// fulfilment allocation. We include enough detail for the consumer to act without a
/// callback to the API, but not so much that the message becomes a second source of truth.
/// </summary>
public sealed record OrderCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required Guid CustomerId { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal TotalAmount { get; init; }
    public required int LineItemCount { get; init; }

    /// <summary>Routing key / queue name this event is published under.</summary>
    public const string RoutingKey = "order.created";
}
