using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SadcOms.Application.Idempotency;

namespace SadcOms.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");

        // The client-supplied key is the primary key — the DB itself prevents duplicate
        // processing under a race, not just the application read-check.
        builder.HasKey(r => r.Key);
        builder.Property(r => r.Key).HasMaxLength(100);

        builder.Property(r => r.RequestTarget).IsRequired().HasMaxLength(200);
        builder.Property(r => r.StatusCode).IsRequired();
        builder.Property(r => r.ResponseBody);
        builder.Property(r => r.CreatedAt).IsRequired();
    }
}
