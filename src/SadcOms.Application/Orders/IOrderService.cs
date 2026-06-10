using SadcOms.Application.Common;
using SadcOms.Domain.Orders;

namespace SadcOms.Application.Orders;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderRequest request, string? correlationId, CancellationToken cancellationToken = default);
    Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderDto>> ListAsync(OrderListFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a status transition. <paramref name="idempotencyKey"/> makes the operation
    /// safe to retry: a repeated key returns the original outcome without re-applying.
    /// </summary>
    Task<OrderDto> UpdateStatusAsync(
        Guid id,
        OrderStatus newStatus,
        string idempotencyKey,
        string requestTarget,
        CancellationToken cancellationToken = default);
}
