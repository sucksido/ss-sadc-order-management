using SadcOms.Domain.Orders;

namespace SadcOms.Application.Orders;

public sealed record CreateOrderLineItemRequest(string ProductSku, int Quantity, decimal UnitPrice);

public sealed record CreateOrderRequest(Guid CustomerId, string CurrencyCode, IReadOnlyList<CreateOrderLineItemRequest> Lines);

public sealed record UpdateOrderStatusRequest(OrderStatus Status);

public sealed record OrderLineItemDto(Guid Id, string ProductSku, int Quantity, decimal UnitPrice, decimal LineTotal)
{
    public static OrderLineItemDto FromEntity(OrderLineItem li) =>
        new(li.Id, li.ProductSku, li.Quantity, li.UnitPrice, li.LineTotal);
}

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    DateTimeOffset CreatedAt,
    string CurrencyCode,
    decimal TotalAmount,
    IReadOnlyList<OrderLineItemDto> Lines)
{
    public static OrderDto FromEntity(Order o) => new(
        o.Id,
        o.CustomerId,
        o.Status.ToString(),
        o.CreatedAt,
        o.CurrencyCode,
        o.TotalAmount,
        o.LineItems.Select(OrderLineItemDto.FromEntity).ToList());

    /// <summary>Projection without line items, used for list endpoints to keep payloads small.</summary>
    public static OrderDto Summary(Order o) => new(
        o.Id, o.CustomerId, o.Status.ToString(), o.CreatedAt, o.CurrencyCode, o.TotalAmount, []);
}

/// <summary>Filter/sort parameters for the order listing endpoint.</summary>
public sealed record OrderListFilter(Guid? CustomerId, OrderStatus? Status, string? Sort);
