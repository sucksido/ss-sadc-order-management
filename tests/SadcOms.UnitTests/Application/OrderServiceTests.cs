using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SadcOms.Application.Common;
using SadcOms.Application.Orders;
using SadcOms.Domain.Common;
using SadcOms.Domain.Customers;
using SadcOms.Domain.Orders;
using SadcOms.Infrastructure.Persistence;
using Xunit;

namespace SadcOms.UnitTests.Application;

public class OrderServiceTests
{
    private static readonly TimeProvider Clock = TimeProvider.System;

    private static async Task<Customer> SeedCustomerAsync(AppDbContext db, string country = "ZA")
    {
        var customer = Customer.Create("Test Customer", "test@example.com", country);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    [Fact]
    public async Task CreateAsync_persists_order_and_writes_outbox_atomically()
    {
        await using var db = TestDbContextFactory.Create();
        var customer = await SeedCustomerAsync(db);
        var service = new OrderService(db, Clock);

        var request = new CreateOrderRequest(customer.Id, "ZAR",
        [
            new CreateOrderLineItemRequest("SKU1", 2, 100m),
            new CreateOrderLineItemRequest("SKU2", 1, 49.99m)
        ]);

        var result = await service.CreateAsync(request, "corr-1");

        result.TotalAmount.Should().Be(249.99m);
        result.Status.Should().Be("Pending");

        (await db.Orders.CountAsync()).Should().Be(1);
        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.Type.Should().Be("OrderCreatedIntegrationEvent");
        outbox.ProcessedAt.Should().BeNull();
        outbox.Payload.Should().Contain(result.Id.ToString());
    }

    [Fact]
    public async Task CreateAsync_throws_when_customer_missing()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new OrderService(db, Clock);

        var request = new CreateOrderRequest(Guid.NewGuid(), "ZAR",
            [new CreateOrderLineItemRequest("SKU", 1, 10m)]);

        var act = () => service.CreateAsync(request, null);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateStatusAsync_applies_a_valid_transition()
    {
        await using var db = TestDbContextFactory.Create();
        var customer = await SeedCustomerAsync(db);
        var service = new OrderService(db, Clock);
        var created = await service.CreateAsync(
            new CreateOrderRequest(customer.Id, "ZAR", [new CreateOrderLineItemRequest("SKU", 1, 10m)]), null);

        var updated = await service.UpdateStatusAsync(created.Id, OrderStatus.Paid, "key-1", "PUT /api/orders/x/status");

        updated.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task UpdateStatusAsync_is_idempotent_for_a_repeated_key()
    {
        await using var db = TestDbContextFactory.Create();
        var customer = await SeedCustomerAsync(db);
        var service = new OrderService(db, Clock);
        var created = await service.CreateAsync(
            new CreateOrderRequest(customer.Id, "ZAR", [new CreateOrderLineItemRequest("SKU", 1, 10m)]), null);

        const string key = "idem-123";
        const string target = "PUT /api/orders/x/status";

        var first = await service.UpdateStatusAsync(created.Id, OrderStatus.Paid, key, target);
        // A second call with the SAME key returns the stored result without re-applying.
        var second = await service.UpdateStatusAsync(created.Id, OrderStatus.Cancelled, key, target);

        first.Status.Should().Be("Paid");
        second.Status.Should().Be("Paid"); // not Cancelled — the replay won
        (await db.IdempotencyRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_rejects_an_illegal_transition()
    {
        await using var db = TestDbContextFactory.Create();
        var customer = await SeedCustomerAsync(db);
        var service = new OrderService(db, Clock);
        var created = await service.CreateAsync(
            new CreateOrderRequest(customer.Id, "ZAR", [new CreateOrderLineItemRequest("SKU", 1, 10m)]), null);

        var act = () => service.UpdateStatusAsync(created.Id, OrderStatus.Fulfilled, "k", "PUT /x");
        await act.Should().ThrowAsync<InvalidStatusTransitionException>();
    }

    [Fact]
    public async Task ListAsync_filters_by_status_and_pages()
    {
        await using var db = TestDbContextFactory.Create();
        var customer = await SeedCustomerAsync(db);
        var service = new OrderService(db, Clock);

        for (var i = 0; i < 3; i++)
        {
            await service.CreateAsync(
                new CreateOrderRequest(customer.Id, "ZAR", [new CreateOrderLineItemRequest("SKU", 1, 10m)]), null);
        }

        var page = await service.ListAsync(new OrderListFilter(customer.Id, OrderStatus.Pending, null), new PageRequest(1, 2));

        page.TotalCount.Should().Be(3);
        page.Items.Should().HaveCount(2);
        page.HasNextPage.Should().BeTrue();
    }
}
