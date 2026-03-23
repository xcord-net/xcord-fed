using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for TwoFactorBackupCode.
/// </summary>
public sealed class TwoFactorBackupCodeConfiguration : IEntityTypeConfiguration<TwoFactorBackupCode>
{
    public void Configure(EntityTypeBuilder<TwoFactorBackupCode> builder)
    {
        // Table name
        builder.ToTable("two_factor_backup_codes");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(tfbc => tfbc.Id);
        builder.Property(tfbc => tfbc.Id)
            .ValueGeneratedNever();

        // CodeHash (required, BCrypt hashes are up to 128 chars)
        builder.Property(tfbc => tfbc.CodeHash)
            .IsRequired()
            .HasMaxLength(128);

        // UserId foreign key (Cascade delete)
        builder.HasOne(tfbc => tfbc.User)
            .WithMany()
            .HasForeignKey(tfbc => tfbc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on UserId for fast lookups
        builder.HasIndex(tfbc => tfbc.UserId);

        // CreatedAt (required)
        builder.Property(tfbc => tfbc.CreatedAt)
            .IsRequired();

        // Suppress EF Core warning: required principal User has a global query filter
        builder.HasQueryFilter(tfbc => tfbc.User!.DeletedAt == null);
    }
}
