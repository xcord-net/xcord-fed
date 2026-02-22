using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for ScheduledEvent.
/// </summary>
public sealed class ScheduledEventConfiguration : IEntityTypeConfiguration<ScheduledEvent>
{
    public void Configure(EntityTypeBuilder<ScheduledEvent> builder)
    {
        // Table name
        builder.ToTable("scheduled_events");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        // ServerId (required, FK to Server with Cascade)
        builder.Property(e => e.ServerId)
            .IsRequired();

        builder.HasOne(e => e.Server)
            .WithMany()
            .HasForeignKey(e => e.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // CreatorId (required, FK to User with Restrict)
        builder.Property(e => e.CreatorId)
            .IsRequired();

        builder.HasOne(e => e.Creator)
            .WithMany()
            .HasForeignKey(e => e.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ChannelId (optional, FK to Channel with SetNull)
        builder.Property(e => e.ChannelId);

        builder.HasOne(e => e.Channel)
            .WithMany()
            .HasForeignKey(e => e.ChannelId)
            .OnDelete(DeleteBehavior.SetNull);

        // Name (required, max 100)
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Description (optional, max 1000)
        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        // Location (optional, max 100)
        builder.Property(e => e.Location)
            .HasMaxLength(100);

        // ImageUrl (optional, max 512)
        builder.Property(e => e.ImageUrl)
            .HasMaxLength(512);

        // ScheduledStartTime (required)
        builder.Property(e => e.ScheduledStartTime)
            .IsRequired();

        // ScheduledEndTime (optional)
        builder.Property(e => e.ScheduledEndTime);

        // Status (required, default Scheduled)
        builder.Property(e => e.Status)
            .IsRequired()
            .HasDefaultValue(Entities.EventStatus.Scheduled);

        // InterestedCount (denormalized, default 0)
        builder.Property(e => e.InterestedCount)
            .IsRequired()
            .HasDefaultValue(0);

        // NotificationSent (default false)
        builder.Property(e => e.NotificationSent)
            .IsRequired()
            .HasDefaultValue(false);

        // Timestamps
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(e => e.DeletedAt);

        // Indexes
        builder.HasIndex(e => e.ServerId);
        builder.HasIndex(e => new { e.ServerId, e.Status });
        builder.HasIndex(e => e.ScheduledStartTime);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
