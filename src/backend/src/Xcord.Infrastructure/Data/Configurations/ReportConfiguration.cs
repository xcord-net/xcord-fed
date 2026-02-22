using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Report.
/// </summary>
public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        // Table name
        builder.ToTable("reports");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        // Foreign keys
        builder.Property(r => r.ServerId)
            .IsRequired();

        builder.Property(r => r.ReporterId)
            .IsRequired();

        builder.Property(r => r.ReportedUserId);

        builder.Property(r => r.ReportedMessageId);

        // Reason (required, max 1000)
        builder.Property(r => r.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        // Status (required, default Pending)
        builder.Property(r => r.Status)
            .IsRequired();

        builder.Property(r => r.ReviewedById);

        // ReviewNotes (optional, max 1000)
        builder.Property(r => r.ReviewNotes)
            .HasMaxLength(1000);

        // CreatedAt (required)
        builder.Property(r => r.CreatedAt)
            .IsRequired();

        // DeletedAt (soft delete)
        builder.Property(r => r.DeletedAt);

        // Indexes
        builder.HasIndex(r => r.ServerId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.ReporterId);

        // Navigation properties
        builder.HasOne(r => r.Server)
            .WithMany()
            .HasForeignKey(r => r.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ReportedUser)
            .WithMany()
            .HasForeignKey(r => r.ReportedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.ReportedMessage)
            .WithMany()
            .HasForeignKey(r => r.ReportedMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.ReviewedBy)
            .WithMany()
            .HasForeignKey(r => r.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
