using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for NotificationSetting.
/// </summary>
public sealed class NotificationSettingConfiguration : IEntityTypeConfiguration<NotificationSetting>
{
    public void Configure(EntityTypeBuilder<NotificationSetting> builder)
    {
        // Table name
        builder.ToTable("notification_settings");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(ns => ns.Id);
        builder.Property(ns => ns.Id)
            .ValueGeneratedNever();

        // UserId (FK to User with Cascade)
        builder.HasOne(ns => ns.User)
            .WithMany()
            .HasForeignKey(ns => ns.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ServerId (FK to Server with Cascade, nullable)
        builder.HasOne(ns => ns.Server)
            .WithMany()
            .HasForeignKey(ns => ns.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChannelId (FK to Channel with Cascade, nullable)
        builder.HasOne(ns => ns.Channel)
            .WithMany()
            .HasForeignKey(ns => ns.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Level (required, default All)
        builder.Property(ns => ns.Level)
            .IsRequired()
            .HasDefaultValue(Entities.NotificationLevel.All);

        // SuppressEveryone (required, default false)
        builder.Property(ns => ns.SuppressEveryone)
            .IsRequired()
            .HasDefaultValue(false);

        // SuppressRoles (required, default false)
        builder.Property(ns => ns.SuppressRoles)
            .IsRequired()
            .HasDefaultValue(false);

        // MuteUntil (nullable)
        builder.Property(ns => ns.MuteUntil);

        // Unique index on (UserId, ServerId, ChannelId) - only one setting per scope
        builder.HasIndex(ns => new { ns.UserId, ns.ServerId, ns.ChannelId })
            .IsUnique();
    }
}
