namespace SadcOms.Application.Reports;

public sealed record OrderZarLine(
    Guid OrderId,
    string OriginalCurrency,
    decimal OriginalTotal,
    decimal FxRate,
    decimal ZarTotal);

public sealed record OrdersZarReport(
    string TargetCurrency,
    DateTimeOffset GeneratedAt,
    int OrderCount,
    decimal GrandTotalZar,
    IReadOnlyList<OrderZarLine> Orders);
