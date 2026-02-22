using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for User.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Table name
        builder.ToTable("users");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        // Username (required, max 32, unique index)
        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(32);
        builder.HasIndex(u => u.Username)
            .IsUnique();

        // DisplayName (required, max 32)
        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(32);

        // Email (encrypted bytea column)
        builder.Property(u => u.Email)
            .IsRequired()
            .HasColumnType("bytea");

        // EmailHash (HMAC-SHA256 blind index, unique constraint)
        builder.Property(u => u.EmailHash)
            .IsRequired()
            .HasColumnType("bytea");
        builder.HasIndex(u => u.EmailHash)
            .IsUnique();

        // PasswordHash (required, max 128, BCrypt)
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(128);

        // AvatarUrl (optional, max 512)
        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(512);

        // Bio (optional, max 190)
        builder.Property(u => u.Bio)
            .HasMaxLength(190);

        // Status (enum, default Offline)
        builder.Property(u => u.Status)
            .IsRequired();

        // CustomStatus (optional, max 128)
        builder.Property(u => u.CustomStatus)
            .HasMaxLength(128);

        // Booleans (all with defaults)
        builder.Property(u => u.IsBot)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.IsAdmin)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.IsDisabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.EmailConfirmed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.TwoFactorEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        // Timestamps
        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.LastLoginAt);

        // Scheduled deletion timestamp (optional)
        builder.Property(u => u.ScheduledDeletionAt);

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(u => u.DeletedAt);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
