using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Timeout.
/// </summary>
public sealed class TimeoutConfiguration : IEntityTypeConfiguration<Entities.Timeout>
{
    public void Configure(EntityTypeBuilder<Entities.Timeout> builder)
    {
        // Table name
        builder.ToTable("timeouts");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        // Foreign keys
        builder.Property(t => t.UserId)
            .IsRequired();

        builder.Property(t => t.ServerId)
            .IsRequired();

        builder.Property(t => t.ModeratorId);

        // ExpiresAt (required)
        builder.Property(t => t.ExpiresAt)
            .IsRequired();

        // Reason (optional, max 512)
        builder.Property(t => t.Reason)
            .HasMaxLength(512);

        // CreatedAt (required)
        builder.Property(t => t.CreatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(t => new { t.ServerId, t.UserId });
        builder.HasIndex(t => t.ExpiresAt);

        // Navigation properties
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Server)
            .WithMany()
            .HasForeignKey(t => t.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Moderator)
            .WithMany()
            .HasForeignKey(t => t.ModeratorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
