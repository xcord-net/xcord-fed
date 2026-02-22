using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for StickerPack.
/// </summary>
public sealed class StickerPackConfiguration : IEntityTypeConfiguration<StickerPack>
{
    public void Configure(EntityTypeBuilder<StickerPack> builder)
    {
        // Table name
        builder.ToTable("sticker_packs");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        // ServerId (nullable, FK to Server with Cascade)
        builder.Property(p => p.ServerId);

        builder.HasOne(p => p.Server)
            .WithMany()
            .HasForeignKey(p => p.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Name (required, max 100)
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Description (optional, max 200)
        builder.Property(p => p.Description)
            .HasMaxLength(200);

        // Timestamps
        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(p => p.DeletedAt);

        // Index for server-scoped queries
        builder.HasIndex(p => p.ServerId);

        // Navigation to Stickers
        builder.HasMany(p => p.Stickers)
            .WithOne(s => s.StickerPack)
            .HasForeignKey(s => s.StickerPackId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
