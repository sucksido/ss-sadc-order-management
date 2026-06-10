using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SadcOms.Domain.Customers;

namespace SadcOms.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(320);
        builder.Property(c => c.CountryCode).IsRequired().HasMaxLength(2).IsFixedLength();
        builder.Property(c => c.CreatedAt).IsRequired();

        // Email is the natural identifier for a customer; enforce uniqueness at the DB level.
        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasIndex(c => c.Name); // supports the search-by-name list query
    }
}
