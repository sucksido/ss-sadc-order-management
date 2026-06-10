using FluentAssertions;
using SadcOms.Application.Abstractions;
using SadcOms.Application.Orders;
using SadcOms.Application.Reports;
using SadcOms.Domain.Customers;
using SadcOms.Infrastructure.Persistence;
using Xunit;

namespace SadcOms.UnitTests.Application;

public class ZarReportServiceTests
{
    // Deterministic stand-in for the FX provider: USD -> ZAR at 18, everything else 1:1.
    private sealed class FixedRateProvider : IFxRateProvider
    {
        public int CallCount { get; private set; }

        public Task<decimal> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
        {
            CallCount++;
            var rate = fromCurrency == "USD" ? 18m : 1m;
            return Task.FromResult(rate);
        }
    }

    [Fact]
    public async Task GetOrdersInZarAsync_converts_and_sums_totals()
    {
        await using var db = TestDbContextFactory.Create();
        var fx = new FixedRateProvider();

        var za = Customer.Create("ZA Customer", "za@example.com", "ZA");
        var zw = Customer.Create("ZW Customer", "zw@example.com", "ZW");
        db.Customers.AddRange(za, zw);
        await db.SaveChangesAsync();

        var orders = new OrderService(db, TimeProvider.System);
        await orders.CreateAsync(new CreateOrderRequest(za.Id, "ZAR", [new CreateOrderLineItemRequest("S", 1, 100m)]), null);
        await orders.CreateAsync(new CreateOrderRequest(zw.Id, "USD", [new CreateOrderLineItemRequest("S", 1, 10m)]), null);

        var report = await new ZarReportService(db, fx).GetOrdersInZarAsync(null, null);

        report.TargetCurrency.Should().Be("ZAR");
        report.OrderCount.Should().Be(2);
        // 100 ZAR (x1) + 10 USD (x18 = 180 ZAR) = 280 ZAR
        report.GrandTotalZar.Should().Be(280m);
    }

    [Fact]
    public async Task GetOrdersInZarAsync_requests_one_rate_per_distinct_currency()
    {
        await using var db = TestDbContextFactory.Create();
        var fx = new FixedRateProvider();

        var za = Customer.Create("ZA Customer", "za@example.com", "ZA");
        db.Customers.Add(za);
        await db.SaveChangesAsync();

        var orders = new OrderService(db, TimeProvider.System);
        for (var i = 0; i < 5; i++)
        {
            await orders.CreateAsync(new CreateOrderRequest(za.Id, "ZAR", [new CreateOrderLineItemRequest("S", 1, 10m)]), null);
        }

        await new ZarReportService(db, fx).GetOrdersInZarAsync(null, null);

        // Five ZAR orders, but only one distinct currency -> a single rate lookup.
        fx.CallCount.Should().Be(1);
    }
}
