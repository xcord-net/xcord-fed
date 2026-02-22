using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Sticker.
/// </summary>
public sealed class StickerConfiguration : IEntityTypeConfiguration<Sticker>
{
    public void Configure(EntityTypeBuilder<Sticker> builder)
    {
        // Table name
        builder.ToTable("stickers");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        // StickerPackId (required, FK to StickerPack with Cascade)
        builder.Property(s => s.StickerPackId)
            .IsRequired();

        builder.HasOne(s => s.StickerPack)
            .WithMany(p => p.Stickers)
            .HasForeignKey(s => s.StickerPackId)
            .OnDelete(DeleteBehavior.Cascade);

        // Name (required, max 32)
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(32);

        // Tags (optional, max 200)
        builder.Property(s => s.Tags)
            .HasMaxLength(200);

        // ImageUrl (required, max 512)
        builder.Property(s => s.ImageUrl)
            .IsRequired()
            .HasMaxLength(512);

        // S3Key (required, max 512)
        builder.Property(s => s.S3Key)
            .IsRequired()
            .HasMaxLength(512);

        // Timestamps
        builder.Property(s => s.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(s => s.DeletedAt);

        // Index for pack-scoped queries
        builder.HasIndex(s => s.StickerPackId);
    }
}
