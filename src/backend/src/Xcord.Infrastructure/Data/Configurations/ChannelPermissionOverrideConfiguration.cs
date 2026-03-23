using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for ChannelPermissionOverride.
/// </summary>
public sealed class ChannelPermissionOverrideConfiguration : IEntityTypeConfiguration<ChannelPermissionOverride>
{
    public void Configure(EntityTypeBuilder<ChannelPermissionOverride> builder)
    {
        // Table name
        builder.ToTable("channel_permission_overrides");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(cpo => cpo.Id);
        builder.Property(cpo => cpo.Id)
            .ValueGeneratedNever();

        // ChannelId (required, FK to Channel with Cascade)
        builder.Property(cpo => cpo.ChannelId)
            .IsRequired();

        builder.HasOne(cpo => cpo.Channel)
            .WithMany(c => c.PermissionOverrides)
            .HasForeignKey(cpo => cpo.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // TargetType (required, enum)
        builder.Property(cpo => cpo.TargetType)
            .IsRequired()
            .HasConversion<int>();

        // TargetId (required, polymorphic - no FK)
        builder.Property(cpo => cpo.TargetId)
            .IsRequired();

        // Allow (required, bitfield)
        builder.Property(cpo => cpo.Allow)
            .IsRequired();

        // Deny (required, bitfield)
        builder.Property(cpo => cpo.Deny)
            .IsRequired();

        // Unique index: one override per (channel, target type, target ID)
        builder.HasIndex(cpo => new { cpo.ChannelId, cpo.TargetType, cpo.TargetId })
            .IsUnique();

        // Index for querying all overrides in a channel
        builder.HasIndex(cpo => cpo.ChannelId);

        // Suppress EF Core warning: required principal Channel has a global query filter
        builder.HasQueryFilter(cpo => cpo.Channel!.DeletedAt == null);
    }
}
