using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for TwoFactorCode.
/// </summary>
public sealed class TwoFactorCodeConfiguration : IEntityTypeConfiguration<TwoFactorCode>
{
    public void Configure(EntityTypeBuilder<TwoFactorCode> builder)
    {
        // Table name
        builder.ToTable("two_factor_codes");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(tfc => tfc.Id);
        builder.Property(tfc => tfc.Id)
            .ValueGeneratedNever();

        // Code (required, 6 characters)
        builder.Property(tfc => tfc.Code)
            .IsRequired()
            .HasMaxLength(6);

        // FailedAttempts (default 0)
        builder.Property(tfc => tfc.FailedAttempts)
            .HasDefaultValue(0);

        // UserId foreign key (Cascade delete)
        builder.HasOne(tfc => tfc.User)
            .WithMany()
            .HasForeignKey(tfc => tfc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on UserId for fast lookups
        builder.HasIndex(tfc => tfc.UserId);

        // ExpiresAt (required)
        builder.Property(tfc => tfc.ExpiresAt)
            .IsRequired();

        // Index on ExpiresAt for cleanup queries
        builder.HasIndex(tfc => tfc.ExpiresAt);

        // CreatedAt (required)
        builder.Property(tfc => tfc.CreatedAt)
            .IsRequired();
    }
}
