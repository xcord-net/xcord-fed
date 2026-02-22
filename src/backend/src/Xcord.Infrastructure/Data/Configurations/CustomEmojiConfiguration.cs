using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for CustomEmoji.
/// </summary>
public sealed class CustomEmojiConfiguration : IEntityTypeConfiguration<CustomEmoji>
{
    public void Configure(EntityTypeBuilder<CustomEmoji> builder)
    {
        // Table name
        builder.ToTable("custom_emojis");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        // ServerId (required, FK to Server with Cascade)
        builder.Property(e => e.ServerId)
            .IsRequired();

        builder.HasOne(e => e.Server)
            .WithMany()
            .HasForeignKey(e => e.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Name (required, max 32, unique per server)
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(32);

        builder.HasIndex(e => new { e.ServerId, e.Name })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL"); // Unique constraint ignores soft-deleted

        // ImageUrl (required, max 512)
        builder.Property(e => e.ImageUrl)
            .IsRequired()
            .HasMaxLength(512);

        // S3Key (required, max 512)
        builder.Property(e => e.S3Key)
            .IsRequired()
            .HasMaxLength(512);

        // IsAnimated (required, default false)
        builder.Property(e => e.IsAnimated)
            .IsRequired()
            .HasDefaultValue(false);

        // CreatorId (required, FK to User with Restrict)
        builder.Property(e => e.CreatorId)
            .IsRequired();

        builder.HasOne(e => e.Creator)
            .WithMany()
            .HasForeignKey(e => e.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Timestamps
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(e => e.DeletedAt);

        // Index for server-scoped queries
        builder.HasIndex(e => e.ServerId);
    }
}
