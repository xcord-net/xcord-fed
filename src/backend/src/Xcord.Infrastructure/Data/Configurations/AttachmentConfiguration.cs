using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Attachment.
/// </summary>
public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        // Table name
        builder.ToTable("attachments");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        // MessageId (optional FK to Message with Cascade - nullable for pre-message uploads)
        builder.Property(a => a.MessageId);

        builder.HasOne(a => a.Message)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasIndex(a => a.MessageId);

        // FileName (required, max 256)
        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(256);

        // ContentType (required, max 128)
        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(128);

        // FileSize (required)
        builder.Property(a => a.FileSize)
            .IsRequired();

        // S3Key (required, max 512)
        builder.Property(a => a.S3Key)
            .IsRequired()
            .HasMaxLength(512);

        // Width (nullable)
        builder.Property(a => a.Width);

        // Height (nullable)
        builder.Property(a => a.Height);

        // ThumbnailS3Key (nullable, max 512)
        builder.Property(a => a.ThumbnailS3Key)
            .HasMaxLength(512);

        // IsConfirmed (required, default false)
        builder.Property(a => a.IsConfirmed)
            .IsRequired()
            .HasDefaultValue(false);

        // CreatedByUserId (nullable - null for legacy attachments)
        builder.Property(a => a.CreatedByUserId);

        // Timestamps
        builder.Property(a => a.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(a => a.DeletedAt);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
