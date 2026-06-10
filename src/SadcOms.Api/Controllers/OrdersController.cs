using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadcOms.Api.Observability;
using SadcOms.Application.Common;
using SadcOms.Application.Orders;
using SadcOms.Domain.Orders;

namespace SadcOms.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    /// <summary>
    /// Creates an order. The total is computed server-side and an OrderCreated event is written
    /// to the outbox in the same transaction for reliable downstream publishing.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var correlationId = HttpContext.Items[CorrelationId.ItemKey]?.ToString();
        var created = await orders.CreateAsync(request, correlationId, ct);
        Telemetry.OrdersCreated.Add(1);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>Lists orders filtered by customer/status, paged and sorted (sort: createdAt|total, _asc/_desc).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> List(
        [FromQuery] Guid? customerId,
        [FromQuery] OrderStatus? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sort,
        CancellationToken ct)
    {
        var filter = new OrderListFilter(customerId, status, sort);
        var result = await orders.ListAsync(filter, new PageRequest(page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// Transitions an order's status. Requires an Idempotency-Key header so a retried request
    /// returns the original outcome instead of re-applying (or rejecting) the transition.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing Idempotency-Key",
                Detail = "The Idempotency-Key header is required for status updates.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var requestTarget = $"PUT {Request.Path}";
        var updated = await orders.UpdateStatusAsync(id, request.Status, idempotencyKey, requestTarget, ct);
        Telemetry.OrderStatusChanged.Add(1);
        return Ok(updated);
    }
}
