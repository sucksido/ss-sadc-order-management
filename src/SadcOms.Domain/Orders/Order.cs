using SadcOms.Domain.Common;
using SadcOms.Domain.Regional;

namespace SadcOms.Domain.Orders;

/// <summary>
/// Order aggregate root. Owns its line items and is the single place where the order
/// total is computed and where lifecycle transitions are validated. Nothing outside this
/// type may set <see cref="TotalAmount"/> or <see cref="Status"/> directly, which is what
/// keeps "TotalAmount is always server-calculated" true by construction.
/// </summary>
public sealed class Order
{
    private readonly List<OrderLineItem> _lineItems = [];

    private Order()
    {
        CurrencyCode = null!;
        RowVersion = [];
    }

    private Order(Guid id, Guid customerId, string currencyCode, DateTimeOffset createdAt)
    {
        Id = id;
        CustomerId = customerId;
        CurrencyCode = currencyCode;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
        RowVersion = [];
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CurrencyCode { get; private set; }
    public decimal TotalAmount { get; private set; }

    /// <summary>
    /// SQL Server <c>rowversion</c> used for optimistic concurrency. EF maps this as a
    /// concurrency token so two clients editing the same order cannot silently clobber
    /// each other — the second write fails with a concurrency exception.
    /// </summary>
    public byte[] RowVersion { get; private set; }

    public IReadOnlyCollection<OrderLineItem> LineItems => _lineItems.AsReadOnly();

    /// <summary>
    /// Creates a Pending order for a customer in a given country/currency. The currency is
    /// validated against the customer's country using SADC/CMA rules, line items are
    /// validated individually, and the total is computed here on the server.
    /// </summary>
    public static Order Create(
        Guid customerId,
        string customerCountryCode,
        string currencyCode,
        IEnumerable<(string Sku, int Quantity, decimal UnitPrice)> lines,
        TimeProvider? clock = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("CustomerId is required.");
        }

        var pairing = SadcRegion.ValidatePairing(customerCountryCode, currencyCode);
        if (!pairing.IsValid)
        {
            throw new DomainException(pairing.Error!);
        }

        var materialised = lines?.ToList() ?? [];
        if (materialised.Count == 0)
        {
            throw new DomainException("An order must contain at least one line item.");
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var order = new Order(Guid.NewGuid(), customerId, currencyCode.Trim().ToUpperInvariant(), now);

        foreach (var (sku, quantity, unitPrice) in materialised)
        {
            order._lineItems.Add(new OrderLineItem(sku, quantity, unitPrice));
        }

        order.RecalculateTotal();
        return order;
    }

    /// <summary>TotalAmount = Σ (Quantity × UnitPrice), always recomputed from line items.</summary>
    public void RecalculateTotal()
    {
        TotalAmount = Money.Round(_lineItems.Sum(li => li.Quantity * li.UnitPrice));
    }

    public bool CanTransitionTo(OrderStatus target) => AllowedTransitions(Status).Contains(target);

    /// <summary>
    /// Applies a lifecycle transition, throwing if the move is not permitted. Re-applying
    /// the current status is treated as a no-op so retried/idempotent calls are safe.
    /// </summary>
    public void TransitionTo(OrderStatus target)
    {
        if (target == Status)
        {
            return; // idempotent: requesting the current state changes nothing
        }

        if (!CanTransitionTo(target))
        {
            throw new InvalidStatusTransitionException(Status.ToString(), target.ToString());
        }

        Status = target;
    }

    /// <summary>
    /// The lifecycle state machine. Pending and Paid orders may be cancelled; Fulfilled and
    /// Cancelled are terminal. (Assumption: the brief lists the states in lifecycle order
    /// rather than implying Fulfilled -> Cancelled, which would not be a sensible business
    /// move once goods are dispatched. This is documented in ANSWERS.md.)
    /// </summary>
    private static IReadOnlySet<OrderStatus> AllowedTransitions(OrderStatus current) => current switch
    {
        OrderStatus.Pending => new HashSet<OrderStatus> { OrderStatus.Paid, OrderStatus.Cancelled },
        OrderStatus.Paid => new HashSet<OrderStatus> { OrderStatus.Fulfilled, OrderStatus.Cancelled },
        OrderStatus.Fulfilled => new HashSet<OrderStatus>(),
        OrderStatus.Cancelled => new HashSet<OrderStatus>(),
        _ => new HashSet<OrderStatus>()
    };
}
