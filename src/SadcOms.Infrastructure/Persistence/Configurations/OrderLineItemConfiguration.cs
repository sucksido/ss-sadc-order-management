using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SadcOms.Domain.Orders;

namespace SadcOms.Infrastructure.Persistence.Configurations;

public sealed class OrderLineItemConfiguration : IEntityTypeConfiguration<OrderLineItem>
{
    public void Configure(EntityTypeBuilder<OrderLineItem> builder)
    {
        builder.ToTable("OrderLineItems");
        builder.HasKey(li => li.Id);

        builder.Property(li => li.ProductSku).IsRequired().HasMaxLength(64);
        builder.Property(li => li.Quantity).IsRequired();
        builder.Property(li => li.UnitPrice).IsRequired().HasColumnType("decimal(18,2)");

        // LineTotal is derived in the domain; it is not persisted.
        builder.Ignore(li => li.LineTotal);

        builder.HasIndex(li => li.OrderId);
    }
}
