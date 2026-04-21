using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Broadcast.
/// </summary>
public sealed class BroadcastConfiguration : IEntityTypeConfiguration<Broadcast>
{
    public void Configure(EntityTypeBuilder<Broadcast> builder)
    {
        builder.ToTable("broadcasts");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.ChannelId).IsRequired();
        builder.Property(b => b.HostUserId).IsRequired();

        builder.Property(b => b.EgressJobId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(b => b.LayoutPreset)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(b => b.HlsPlaylistKey)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(b => b.StartedAt).IsRequired();
        builder.Property(b => b.EndedAt);
        builder.Property(b => b.DeletedAt);

        builder.HasOne(b => b.Channel)
            .WithMany()
            .HasForeignKey(b => b.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Host)
            .WithMany()
            .HasForeignKey(b => b.HostUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.ChannelId, b.Status });

        // Soft delete query filter is applied globally in AppDbContext.
    }
}
