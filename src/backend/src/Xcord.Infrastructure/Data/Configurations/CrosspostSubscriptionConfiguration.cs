using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for CrosspostSubscription.
/// </summary>
public sealed class CrosspostSubscriptionConfiguration : IEntityTypeConfiguration<CrosspostSubscription>
{
    public void Configure(EntityTypeBuilder<CrosspostSubscription> builder)
    {
        builder.ToTable("crosspost_subscriptions");

        builder.HasKey(cs => cs.Id);
        builder.Property(cs => cs.Id)
            .ValueGeneratedNever();

        builder.Property(cs => cs.SourceChannelId)
            .IsRequired();

        builder.Property(cs => cs.TargetChannelId)
            .IsRequired();

        builder.Property(cs => cs.SourceServerId)
            .IsRequired();

        builder.Property(cs => cs.TargetServerId)
            .IsRequired();

        builder.Property(cs => cs.CreatedAt)
            .IsRequired();

        builder.Property(cs => cs.DeletedAt);

        // Prevent duplicate subscriptions for the same source→target pair
        builder.HasIndex(cs => new { cs.SourceChannelId, cs.TargetChannelId })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasOne(cs => cs.SourceChannel)
            .WithMany()
            .HasForeignKey(cs => cs.SourceChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cs => cs.TargetChannel)
            .WithMany()
            .HasForeignKey(cs => cs.TargetChannelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
