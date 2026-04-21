using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for BroadcastStreambot.
/// </summary>
public sealed class BroadcastStreambotConfiguration : IEntityTypeConfiguration<BroadcastStreambot>
{
    public void Configure(EntityTypeBuilder<BroadcastStreambot> builder)
    {
        builder.ToTable("broadcast_streambots");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.BroadcastId).IsRequired();
        builder.Property(b => b.StreamBotId).IsRequired();

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(b => b.LastError)
            .HasMaxLength(1024);

        builder.Property(b => b.StartedAt).IsRequired();
        builder.Property(b => b.EndedAt);

        builder.HasOne(b => b.Broadcast)
            .WithMany(br => br.ActiveStreambots)
            .HasForeignKey(b => b.BroadcastId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.StreamBot)
            .WithMany()
            .HasForeignKey(b => b.StreamBotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.BroadcastId, b.StreamBotId }).IsUnique();
    }
}
