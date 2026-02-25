using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for BotToken.
/// </summary>
public sealed class BotTokenConfiguration : IEntityTypeConfiguration<BotToken>
{
    public void Configure(EntityTypeBuilder<BotToken> builder)
    {
        // Table name
        builder.ToTable("bot_tokens");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(bt => bt.Id);
        builder.Property(bt => bt.Id)
            .ValueGeneratedNever();

        // TokenHash (required, max 128 for SHA-256 hex)
        builder.Property(bt => bt.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        // Index on TokenHash for fast lookups
        builder.HasIndex(bt => bt.TokenHash);

        // UserId foreign key (Restrict delete - prevent deletion of bot user while token exists)
        builder.HasOne(bt => bt.User)
            .WithMany()
            .HasForeignKey(bt => bt.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Name (required, max 100)
        builder.Property(bt => bt.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Permissions (required, bitfield)
        builder.Property(bt => bt.Permissions)
            .IsRequired();

        // IsRevoked (required, default false)
        builder.Property(bt => bt.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        // CreatedAt (required)
        builder.Property(bt => bt.CreatedAt)
            .IsRequired();

        // LastUsedAt (optional)
        builder.Property(bt => bt.LastUsedAt);

        // InteractionEndpointUrl (optional, max 2048)
        builder.Property(bt => bt.InteractionEndpointUrl)
            .HasMaxLength(2048);

        // InteractionSigningKey (optional, bytea — encrypted at rest)
        builder.Property(bt => bt.InteractionSigningKey)
            .HasColumnType("bytea");

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(bt => bt.DeletedAt);

        // Index on UserId for querying all tokens for a bot user
        builder.HasIndex(bt => bt.UserId);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
