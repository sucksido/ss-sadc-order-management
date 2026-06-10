using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SadcOms.Application.Outbox;

namespace SadcOms.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.CorrelationId).HasMaxLength(100);
        builder.Property(m => m.OccurredAt).IsRequired();
        builder.Property(m => m.Attempts).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);

        // The dispatcher polls for unprocessed rows oldest-first. A filtered index keeps that
        // scan tiny once the table grows, because processed rows drop out of the index.
        builder.HasIndex(m => new { m.ProcessedAt, m.OccurredAt })
            .HasDatabaseName("IX_OutboxMessages_Unprocessed")
            .HasFilter("[ProcessedAt] IS NULL");
    }
}
