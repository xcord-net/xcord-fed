using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Webhook.
/// </summary>
public sealed class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> builder)
    {
        // Table name
        builder.ToTable("webhooks");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .ValueGeneratedNever();

        // Token (required, max 128 - stored in plain text for URL matching)
        builder.Property(w => w.Token)
            .IsRequired()
            .HasMaxLength(128);

        // Unique index on Token for fast lookups
        builder.HasIndex(w => w.Token)
            .IsUnique();

        // ChannelId (required - FK will be added when Channel entity exists)
        builder.Property(w => w.ChannelId)
            .IsRequired();

        // Index on ChannelId for querying all webhooks for a channel
        builder.HasIndex(w => w.ChannelId);

        // Name (required, max 80)
        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(80);

        // AvatarUrl (optional, max 512)
        builder.Property(w => w.AvatarUrl)
            .HasMaxLength(512);

        // CreatedByUserId foreign key (SetNull on user deletion)
        builder.HasOne(w => w.CreatedByUser)
            .WithMany()
            .HasForeignKey(w => w.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // CreatedAt (required)
        builder.Property(w => w.CreatedAt)
            .IsRequired();

        // DeletedAt (soft delete, implements ISoftDeletable)
        builder.Property(w => w.DeletedAt);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
