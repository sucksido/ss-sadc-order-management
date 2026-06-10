using Microsoft.EntityFrameworkCore;
using SadcOms.Application.Abstractions;
using SadcOms.Domain.Common;
using SadcOms.Domain.Orders;
using SadcOms.Domain.Regional;

namespace SadcOms.Application.Reports;

public sealed class ZarReportService(IAppDbContext db, IFxRateProvider fx) : IZarReportService
{
    private const string Target = SadcRegion.AnchorCurrency; // ZAR

    public async Task<OrdersZarReport> GetOrdersInZarAsync(
        OrderStatus? status,
        DateTimeOffset? createdSince,
        CancellationToken cancellationToken = default)
    {
        var query = db.Orders.AsNoTracking();

        if (status is { } s)
        {
            query = query.Where(o => o.Status == s);
        }

        if (createdSince is { } since)
        {
            query = query.Where(o => o.CreatedAt >= since);
        }

        var orders = await query
            .Select(o => new { o.Id, o.CurrencyCode, o.TotalAmount })
            .ToListAsync(cancellationToken);

        // Fetch one rate per distinct currency rather than per order — the provider caches,
        // but de-duplicating here keeps the work O(currencies) instead of O(orders).
        var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var currency in orders.Select(o => o.CurrencyCode).Distinct(StringComparer.Ordinal))
        {
            rates[currency] = await fx.GetRateAsync(currency, Target, cancellationToken);
        }

        var lines = new List<OrderZarLine>(orders.Count);
        decimal grandTotal = 0m;

        foreach (var o in orders)
        {
            var rate = rates[o.CurrencyCode];
            var zar = Money.Round(o.TotalAmount * rate); // round each converted line to the cent
            grandTotal += zar;
            lines.Add(new OrderZarLine(o.Id, o.CurrencyCode, o.TotalAmount, rate, zar));
        }

        return new OrdersZarReport(
            TargetCurrency: Target,
            GeneratedAt: DateTimeOffset.UtcNow,
            OrderCount: lines.Count,
            GrandTotalZar: Money.Round(grandTotal),
            Orders: lines);
    }
}
