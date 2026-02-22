using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for OutboxEvent.
/// </summary>
public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        // Table name
        builder.ToTable("outbox_events");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        // EventType (required, max 200)
        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(200);

        // Payload (jsonb column type)
        builder.Property(e => e.Payload)
            .IsRequired()
            .HasColumnType("jsonb");

        // CreatedAt (required)
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // ProcessedAt (nullable)
        builder.Property(e => e.ProcessedAt);

        // LastAttemptAt (nullable)
        builder.Property(e => e.LastAttemptAt);

        // RetryCount (required, default 0)
        builder.Property(e => e.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Indexes
        // Index on ProcessedAt for cleanup queries
        builder.HasIndex(e => e.ProcessedAt)
            .HasDatabaseName("ix_outbox_events_processed_at");

        // Composite index on (ProcessedAt, CreatedAt) for unprocessed event polling
        builder.HasIndex(e => new { e.ProcessedAt, e.CreatedAt })
            .HasDatabaseName("ix_outbox_events_processed_at_created_at");

        // NOTE: No soft-delete filter - this entity is hard-deleted after retention period
    }
}
