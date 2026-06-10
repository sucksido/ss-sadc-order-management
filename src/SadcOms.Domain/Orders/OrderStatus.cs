namespace SadcOms.Domain.Orders;

/// <summary>
/// Order lifecycle states. Persisted as a string (see EF configuration) rather than an
/// int so the database stays readable and reordering the enum can never corrupt data.
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Fulfilled = 2,
    Cancelled = 3
}
