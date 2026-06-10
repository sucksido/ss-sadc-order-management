using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadcOms.Application.Reports;
using SadcOms.Domain.Orders;

namespace SadcOms.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController(IZarReportService report) : ControllerBase
{
    /// <summary>
    /// Order totals converted to ZAR via the FX provider. Optionally filter by status and a
    /// "since" timestamp (e.g. last 90 days for a finance report).
    /// </summary>
    [HttpGet("orders/zar")]
    [ProducesResponseType(typeof(OrdersZarReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrdersZarReport>> OrdersInZar(
        [FromQuery] OrderStatus? status,
        [FromQuery] DateTimeOffset? since,
        CancellationToken ct)
    {
        var result = await report.GetOrdersInZarAsync(status, since, ct);
        return Ok(result);
    }
}
