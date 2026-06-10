using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SadcOms.Domain.Customers;
using SadcOms.Domain.Orders;

namespace SadcOms.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        // Store the enum as a readable string instead of an int.
        builder.Property(o => o.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.CurrencyCode).IsRequired().HasMaxLength(3).IsFixedLength();
        builder.Property(o => o.TotalAmount).IsRequired().HasColumnType("decimal(18,2)");

        // SQL Server rowversion -> optimistic concurrency token.
        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.LineItems)
            .WithOne()
            .HasForeignKey(li => li.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Map the encapsulated collection through its backing field (configured after the
        // navigation has been registered above).
        builder.Navigation(o => o.LineItems)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Composite index aligned with the primary list query
        // (filter by CustomerId + Status, order by CreatedAt). See ANSWERS.md SQL section.
        builder.HasIndex(o => new { o.CustomerId, o.Status, o.CreatedAt })
            .HasDatabaseName("IX_Orders_CustomerId_Status_CreatedAt");

        builder.HasIndex(o => new { o.Status, o.CreatedAt })
            .HasDatabaseName("IX_Orders_Status_CreatedAt");
    }
}
