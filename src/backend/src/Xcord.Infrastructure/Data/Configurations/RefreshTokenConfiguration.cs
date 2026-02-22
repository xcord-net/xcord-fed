using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for RefreshToken.
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // Table name
        builder.ToTable("refresh_tokens");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Id)
            .ValueGeneratedNever();

        // TokenHash (required, max 128 for SHA-256 hex)
        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        // Index on TokenHash for fast lookups
        builder.HasIndex(rt => rt.TokenHash);

        // UserId foreign key (Restrict delete)
        builder.HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ExpiresAt (required)
        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        // Index on ExpiresAt for cleanup queries
        builder.HasIndex(rt => rt.ExpiresAt);

        // CreatedAt (required)
        builder.Property(rt => rt.CreatedAt)
            .IsRequired();
    }
}
