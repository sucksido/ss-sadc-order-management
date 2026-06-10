using FluentAssertions;
using SadcOms.Domain.Common;
using SadcOms.Domain.Orders;
using Xunit;

namespace SadcOms.UnitTests.Domain;

public class OrderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static Order NewOrder(params (string Sku, int Qty, decimal Price)[] lines) =>
        Order.Create(CustomerId, "ZA", "ZAR", lines);

    [Fact]
    public void Create_computes_total_from_line_items()
    {
        var order = NewOrder(("SKU1", 3, 149.99m), ("SKU2", 2, 89.50m));

        order.TotalAmount.Should().Be(628.97m);
        order.Status.Should().Be(OrderStatus.Pending);
        order.LineItems.Should().HaveCount(2);
    }

    [Fact]
    public void Create_rejects_an_empty_order()
    {
        var act = () => Order.Create(CustomerId, "ZA", "ZAR", Array.Empty<(string, int, decimal)>());
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    public void Create_rejects_non_positive_quantity(int qty, decimal price)
    {
        var act = () => NewOrder(("SKU", qty, price));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_rejects_negative_unit_price()
    {
        var act = () => NewOrder(("SKU", 1, -0.01m));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_rejects_currency_not_valid_for_country()
    {
        var act = () => Order.Create(CustomerId, "ZA", "USD", new[] { ("SKU", 1, 1m) });
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void TransitionTo_follows_the_happy_path()
    {
        var order = NewOrder(("SKU", 1, 10m));

        order.TransitionTo(OrderStatus.Paid);
        order.Status.Should().Be(OrderStatus.Paid);

        order.TransitionTo(OrderStatus.Fulfilled);
        order.Status.Should().Be(OrderStatus.Fulfilled);
    }

    [Fact]
    public void TransitionTo_allows_cancellation_before_fulfilment()
    {
        var order = NewOrder(("SKU", 1, 10m));
        order.TransitionTo(OrderStatus.Paid);

        order.TransitionTo(OrderStatus.Cancelled);
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void TransitionTo_rejects_illegal_moves()
    {
        var order = NewOrder(("SKU", 1, 10m));

        var act = () => order.TransitionTo(OrderStatus.Fulfilled); // Pending -> Fulfilled skips Paid
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void TransitionTo_to_terminal_state_is_blocked()
    {
        var order = NewOrder(("SKU", 1, 10m));
        order.TransitionTo(OrderStatus.Cancelled);

        var act = () => order.TransitionTo(OrderStatus.Paid);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void TransitionTo_same_status_is_idempotent_noop()
    {
        var order = NewOrder(("SKU", 1, 10m));
        order.TransitionTo(OrderStatus.Pending);
        order.Status.Should().Be(OrderStatus.Pending);
    }
}
