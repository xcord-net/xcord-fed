using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for AutomodRule.
/// </summary>
public sealed class AutomodRuleConfiguration : IEntityTypeConfiguration<AutomodRule>
{
    public void Configure(EntityTypeBuilder<AutomodRule> builder)
    {
        // Table name
        builder.ToTable("automod_rules");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        // ServerId (required, FK to Server with Cascade)
        builder.Property(r => r.ServerId)
            .IsRequired();

        builder.HasOne(r => r.Server)
            .WithMany()
            .HasForeignKey(r => r.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.ServerId);

        // Name (required, max 100)
        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Enabled (required, default true)
        builder.Property(r => r.Enabled)
            .IsRequired()
            .HasDefaultValue(true);

        // TriggerType (required, enum stored as int)
        builder.Property(r => r.TriggerType)
            .IsRequired()
            .HasConversion<int>();

        // TriggerConfig (required, jsonb)
        builder.Property(r => r.TriggerConfig)
            .IsRequired()
            .HasColumnType("jsonb");

        // ActionType (required, enum stored as int)
        builder.Property(r => r.ActionType)
            .IsRequired()
            .HasConversion<int>();

        // ActionConfig (nullable, jsonb)
        builder.Property(r => r.ActionConfig)
            .HasColumnType("jsonb");

        // ExemptRoleIds (nullable)
        builder.Property(r => r.ExemptRoleIds);

        // ExemptChannelIds (nullable)
        builder.Property(r => r.ExemptChannelIds);

        // ExemptBots (required, default true)
        builder.Property(r => r.ExemptBots)
            .IsRequired()
            .HasDefaultValue(true);

        // Timestamps
        builder.Property(r => r.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(r => r.DeletedAt);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
