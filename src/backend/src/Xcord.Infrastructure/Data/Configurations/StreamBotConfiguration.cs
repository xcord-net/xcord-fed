using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for StreamBot.
/// </summary>
public sealed class StreamBotConfiguration : IEntityTypeConfiguration<StreamBot>
{
    public void Configure(EntityTypeBuilder<StreamBot> builder)
    {
        builder.ToTable("stream_bots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.ChannelId).IsRequired();
        builder.Property(s => s.CreatedByUserId).IsRequired();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Platform)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(s => s.RtmpUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.EncryptedStreamKey)
            .IsRequired();

        builder.Property(s => s.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.DeletedAt);

        builder.HasOne(s => s.Channel)
            .WithMany()
            .HasForeignKey(s => s.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.CreatedBy)
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.ChannelId);

        // Soft delete query filter is applied globally in AppDbContext.
    }
}
