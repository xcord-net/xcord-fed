using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Embed.
/// </summary>
public sealed class EmbedConfiguration : IEntityTypeConfiguration<Embed>
{
    public void Configure(EntityTypeBuilder<Embed> builder)
    {
        // Table name
        builder.ToTable("embeds");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        // MessageId (required, FK to Message with Cascade)
        builder.Property(e => e.MessageId)
            .IsRequired();

        builder.HasOne(e => e.Message)
            .WithMany()
            .HasForeignKey(e => e.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.MessageId);

        // Url (required, max 2048)
        builder.Property(e => e.Url)
            .IsRequired()
            .HasMaxLength(2048);

        // Title (optional, max 256)
        builder.Property(e => e.Title)
            .HasMaxLength(256);

        // Description (optional, max 4096)
        builder.Property(e => e.Description)
            .HasMaxLength(4096);

        // ImageUrl (optional, max 512)
        builder.Property(e => e.ImageUrl)
            .HasMaxLength(512);

        // ImageS3Key (optional, max 512)
        builder.Property(e => e.ImageS3Key)
            .HasMaxLength(512);

        // SiteName (optional, max 128)
        builder.Property(e => e.SiteName)
            .HasMaxLength(128);

        // Color (optional, max 7)
        builder.Property(e => e.Color)
            .HasMaxLength(7);

        // Position (required)
        builder.Property(e => e.Position)
            .IsRequired();

        // DeletedAt (nullable, for soft delete)
        builder.Property(e => e.DeletedAt)
            .IsRequired(false);
    }
}
