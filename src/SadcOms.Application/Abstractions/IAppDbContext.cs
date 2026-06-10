using Microsoft.EntityFrameworkCore;
using SadcOms.Application.Idempotency;
using SadcOms.Application.Outbox;
using SadcOms.Domain.Customers;
using SadcOms.Domain.Orders;

namespace SadcOms.Application.Abstractions;

/// <summary>
/// The persistence surface the application layer depends on. Backed by EF Core in
/// infrastructure. Exposing <see cref="DbSet{T}"/> lets use cases write expressive LINQ
/// while keeping the concrete <c>DbContext</c> (and provider) out of the application.
/// </summary>
public interface IAppDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<Order> Orders { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
