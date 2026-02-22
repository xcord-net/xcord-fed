using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for PasswordResetToken.
/// </summary>
public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        // Table name
        builder.ToTable("password_reset_tokens");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(prt => prt.Id);
        builder.Property(prt => prt.Id)
            .ValueGeneratedNever();

        // TokenHash (required, max 128 for SHA-256 hex)
        builder.Property(prt => prt.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        // Index on TokenHash for fast lookups
        builder.HasIndex(prt => prt.TokenHash);

        // UserId foreign key (Cascade delete)
        builder.HasOne(prt => prt.User)
            .WithMany()
            .HasForeignKey(prt => prt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ExpiresAt (required)
        builder.Property(prt => prt.ExpiresAt)
            .IsRequired();

        // Index on ExpiresAt for cleanup queries
        builder.HasIndex(prt => prt.ExpiresAt);

        // CreatedAt (required)
        builder.Property(prt => prt.CreatedAt)
            .IsRequired();
    }
}
