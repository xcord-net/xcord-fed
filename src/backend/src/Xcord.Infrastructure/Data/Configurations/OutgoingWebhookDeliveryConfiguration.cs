using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for OutgoingWebhookDelivery.
/// Note: This entity is NOT soft-deleted — it is hard-deleted by the cleanup service.
/// </summary>
public sealed class OutgoingWebhookDeliveryConfiguration : IEntityTypeConfiguration<OutgoingWebhookDelivery>
{
    public void Configure(EntityTypeBuilder<OutgoingWebhookDelivery> builder)
    {
        // Table name
        builder.ToTable("outgoing_webhook_deliveries");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .ValueGeneratedNever();

        // WebhookId (required)
        builder.Property(d => d.WebhookId)
            .IsRequired();

        // EventType (required, max 100)
        builder.Property(d => d.EventType)
            .IsRequired()
            .HasMaxLength(100);

        // Payload (required, jsonb)
        builder.Property(d => d.Payload)
            .IsRequired()
            .HasColumnType("jsonb");

        // AttemptCount (required, default 0)
        builder.Property(d => d.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        // LastAttemptAt (nullable)
        builder.Property(d => d.LastAttemptAt);

        // NextAttemptAt (nullable)
        builder.Property(d => d.NextAttemptAt);

        // Status (required, stored as int)
        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<int>();

        // LastHttpStatus (nullable int)
        builder.Property(d => d.LastHttpStatus);

        // LastError (optional, max 1000)
        builder.Property(d => d.LastError)
            .HasMaxLength(1000);

        // CreatedAt (required)
        builder.Property(d => d.CreatedAt)
            .IsRequired();

        // OutgoingWebhook FK — cascade delete (delivery records removed with the webhook)
        // Note: IgnoreQueryFilters used in delivery service to load even soft-deleted webhooks
        builder.HasOne(d => d.Webhook)
            .WithMany()
            .HasForeignKey(d => d.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite index on (WebhookId, Status) for efficient delivery polling
        builder.HasIndex(d => new { d.WebhookId, d.Status })
            .HasDatabaseName("ix_outgoing_webhook_deliveries_webhook_id_status");

        // Index on (Status, NextAttemptAt) for the delivery poller query
        builder.HasIndex(d => new { d.Status, d.NextAttemptAt })
            .HasDatabaseName("ix_outgoing_webhook_deliveries_status_next_attempt");

        // Index on CreatedAt for cleanup queries
        builder.HasIndex(d => d.CreatedAt)
            .HasDatabaseName("ix_outgoing_webhook_deliveries_created_at");

        // NOTE: No soft-delete filter — this entity is hard-deleted after retention period
    }
}
