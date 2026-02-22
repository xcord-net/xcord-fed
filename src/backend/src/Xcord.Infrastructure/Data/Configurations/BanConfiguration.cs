using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Ban.
/// </summary>
public sealed class BanConfiguration : IEntityTypeConfiguration<Ban>
{
    public void Configure(EntityTypeBuilder<Ban> builder)
    {
        // Table name
        builder.ToTable("bans");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .ValueGeneratedNever();

        // Foreign keys
        builder.Property(b => b.UserId)
            .IsRequired();

        builder.Property(b => b.ServerId)
            .IsRequired();

        builder.Property(b => b.ModeratorId);

        // Reason (optional, max 512)
        builder.Property(b => b.Reason)
            .HasMaxLength(512);

        // DeleteMessageDays (optional, 1-7)
        builder.Property(b => b.DeleteMessageDays);

        // CreatedAt (required)
        builder.Property(b => b.CreatedAt)
            .IsRequired();

        // DeletedAt (soft delete)
        builder.Property(b => b.DeletedAt);

        // Indexes
        builder.HasIndex(b => new { b.ServerId, b.UserId })
            .IsUnique();

        // Navigation properties
        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Server)
            .WithMany()
            .HasForeignKey(b => b.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Moderator)
            .WithMany()
            .HasForeignKey(b => b.ModeratorId)
            .OnDelete(DeleteBehavior.SetNull);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
