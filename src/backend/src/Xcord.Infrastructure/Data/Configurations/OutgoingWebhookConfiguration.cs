using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for OutgoingWebhook.
/// </summary>
public sealed class OutgoingWebhookConfiguration : IEntityTypeConfiguration<OutgoingWebhook>
{
    public void Configure(EntityTypeBuilder<OutgoingWebhook> builder)
    {
        // Table name
        builder.ToTable("outgoing_webhooks");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .ValueGeneratedNever();

        // ServerId (required)
        builder.Property(w => w.ServerId)
            .IsRequired();

        // Index on ServerId for queries by server
        builder.HasIndex(w => w.ServerId);

        // TargetUrl (required, max 2048)
        builder.Property(w => w.TargetUrl)
            .IsRequired()
            .HasMaxLength(2048);

        // Secret (encrypted bytea - raw bytes stored, encryption handled in service layer)
        builder.Property(w => w.Secret)
            .IsRequired()
            .HasColumnType("bytea");

        // EventTypesJson (jsonb array of event type strings)
        builder.Property(w => w.EventTypesJson)
            .IsRequired()
            .HasColumnType("jsonb");

        // IsActive (required, default true)
        builder.Property(w => w.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // CreatedByUserId (required, SetNull on user deletion)
        builder.Property(w => w.CreatedByUserId)
            .IsRequired();

        // CreatedAt (required)
        builder.Property(w => w.CreatedAt)
            .IsRequired();

        // DeletedAt (soft delete, implements ISoftDeletable)
        builder.Property(w => w.DeletedAt);

        // Server FK - cascade delete when server is deleted
        builder.HasOne(w => w.Server)
            .WithMany()
            .HasForeignKey(w => w.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // CreatedByUser FK - SetNull on user deletion
        builder.HasOne(w => w.CreatedByUser)
            .WithMany()
            .HasForeignKey(w => w.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
