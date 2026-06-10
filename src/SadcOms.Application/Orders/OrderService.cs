using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SadcOms.Application.Abstractions;
using SadcOms.Application.Common;
using SadcOms.Application.Idempotency;
using SadcOms.Application.Outbox;
using SadcOms.Contracts;
using SadcOms.Domain.Orders;

namespace SadcOms.Application.Orders;

public sealed class OrderService(IAppDbContext db, TimeProvider clock) : IOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OrderDto> CreateAsync(CreateOrderRequest request, string? correlationId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        // The domain validates the currency against the customer's country (SADC/CMA rules)
        // and computes the total server-side.
        var order = Order.Create(
            customer.Id,
            customer.CountryCode,
            request.CurrencyCode,
            request.Lines.Select(l => (l.ProductSku, l.Quantity, l.UnitPrice)),
            clock);

        db.Orders.Add(order);

        // Outbox write happens in the SAME transaction as the order insert, so the event can
        // never be lost relative to the state change. The dispatcher publishes it afterwards.
        var integrationEvent = new OrderCreatedIntegrationEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            CurrencyCode = order.CurrencyCode,
            TotalAmount = order.TotalAmount,
            LineItemCount = order.LineItems.Count,
            CorrelationId = correlationId
        };

        db.OutboxMessages.Add(new OutboxMessage(
            type: nameof(OrderCreatedIntegrationEvent),
            payload: JsonSerializer.Serialize(integrationEvent, JsonOptions),
            correlationId: correlationId,
            occurredAt: clock.GetUtcNow()));

        await db.SaveChangesAsync(cancellationToken);

        return OrderDto.FromEntity(order);
    }

    public async Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order is null ? null : OrderDto.FromEntity(order);
    }

    public async Task<PagedResult<OrderDto>> ListAsync(OrderListFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = db.Orders.AsNoTracking();

        if (filter.CustomerId is { } customerId)
        {
            query = query.Where(o => o.CustomerId == customerId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(o => o.Status == status);
        }

        var total = await query.LongCountAsync(cancellationToken);

        query = ApplySort(query, filter.Sort);

        var entities = await query
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        // Map after materialisation (summary omits line items, so no Include is needed).
        var items = entities.Select(OrderDto.Summary).ToList();
        return new PagedResult<OrderDto>(items, page.Page, page.PageSize, total);
    }

    public async Task<OrderDto> UpdateStatusAsync(
        Guid id,
        OrderStatus newStatus,
        string idempotencyKey,
        string requestTarget,
        CancellationToken cancellationToken = default)
    {
        // Replay protection: if we have already handled this key, return the stored outcome.
        var existing = await db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey, cancellationToken);

        if (existing is not null)
        {
            if (existing.RequestTarget != requestTarget)
            {
                throw new ConflictException("Idempotency-Key has already been used for a different request.");
            }

            return Deserialize(existing.ResponseBody)
                   ?? throw new ConflictException("Stored idempotent response could not be read.");
        }

        var order = await db.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("Order", id);

        order.TransitionTo(newStatus); // throws InvalidStatusTransitionException on illegal moves

        var result = OrderDto.FromEntity(order);

        db.IdempotencyRecords.Add(new IdempotencyRecord(
            key: idempotencyKey,
            requestTarget: requestTarget,
            statusCode: 200,
            responseBody: JsonSerializer.Serialize(result, JsonOptions),
            createdAt: clock.GetUtcNow()));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion mismatch: another writer changed the order first.
            throw new ConflictException("The order was modified by another request. Reload and retry.");
        }

        return result;
    }

    private static IQueryable<Order> ApplySort(IQueryable<Order> query, string? sort) => sort?.Trim().ToLowerInvariant() switch
    {
        "createdat" or "createdat_asc" => query.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id),
        "total" or "total_desc" => query.OrderByDescending(o => o.TotalAmount).ThenBy(o => o.Id),
        "total_asc" => query.OrderBy(o => o.TotalAmount).ThenBy(o => o.Id),
        // Default: newest first, with Id as a stable tie-breaker for deterministic paging.
        _ => query.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
    };

    private static OrderDto? Deserialize(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<OrderDto>(json, JsonOptions);
}
