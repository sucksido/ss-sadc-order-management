using SadcOms.Domain.Orders;

namespace SadcOms.Application.Reports;

public interface IZarReportService
{
    /// <summary>
    /// Converts order totals to ZAR using the FX provider and returns a summary. Optionally
    /// filters to a status and a created-since window (useful for periodic finance reports).
    /// </summary>
    Task<OrdersZarReport> GetOrdersInZarAsync(
        OrderStatus? status,
        DateTimeOffset? createdSince,
        CancellationToken cancellationToken = default);
}
