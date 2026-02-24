using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for FederationFollow.
/// </summary>
public sealed class FederationFollowConfiguration : IEntityTypeConfiguration<FederationFollow>
{
    public void Configure(EntityTypeBuilder<FederationFollow> builder)
    {
        builder.ToTable("federation_follows");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .ValueGeneratedNever();

        builder.Property(f => f.RemoteInstanceUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(f => f.LocalChannelId)
            .IsRequired();

        builder.Property(f => f.RemoteChannelId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(f => f.RemoteChannelName)
            .HasMaxLength(100);

        builder.Property(f => f.FollowedByUserId)
            .IsRequired();

        builder.Property(f => f.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.Property(f => f.DeletedAt);

        // Unique: one follow per remote channel per local channel
        builder.HasIndex(f => new { f.LocalChannelId, f.RemoteInstanceUrl, f.RemoteChannelId })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(f => f.LocalChannelId);

        builder.HasOne(f => f.LocalChannel)
            .WithMany()
            .HasForeignKey(f => f.LocalChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.FollowedByUser)
            .WithMany()
            .HasForeignKey(f => f.FollowedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
