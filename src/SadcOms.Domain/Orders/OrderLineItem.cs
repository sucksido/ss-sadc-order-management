using SadcOms.Domain.Common;

namespace SadcOms.Domain.Orders;

public sealed class OrderLineItem
{
    private OrderLineItem()
    {
        ProductSku = null!;
    }

    internal OrderLineItem(string productSku, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productSku))
        {
            throw new DomainException("Line item ProductSku is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Line item Quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Line item UnitPrice must be zero or greater.");
        }

        Id = Guid.NewGuid();
        ProductSku = productSku.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string ProductSku { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    /// <summary>Extended price for the line. Rounded to the currency minor unit.</summary>
    public decimal LineTotal => Money.Round(Quantity * UnitPrice);
}
