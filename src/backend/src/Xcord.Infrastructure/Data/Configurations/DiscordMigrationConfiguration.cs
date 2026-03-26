using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class DiscordMigrationConfiguration : IEntityTypeConfiguration<DiscordMigration>
{
    public void Configure(EntityTypeBuilder<DiscordMigration> builder)
    {
        builder.ToTable("discord_migrations");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.DiscordGuildId).IsRequired().HasMaxLength(32);
        builder.Property(m => m.ServerId).IsRequired();

        builder.HasOne(m => m.Server)
            .WithMany()
            .HasForeignKey(m => m.ServerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.ServerId);

        builder.Property(m => m.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
        builder.Property(m => m.CurrentPhase).HasMaxLength(100);
        builder.Property(m => m.CheckpointJson).HasColumnType("jsonb");
        builder.Property(m => m.OptionsJson).IsRequired().HasColumnType("jsonb").HasDefaultValueSql("'{}'");
        builder.Property(m => m.TotalChannels).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.MigratedChannels).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.TotalMessages).IsRequired().HasDefaultValue(0L);
        builder.Property(m => m.MigratedMessages).IsRequired().HasDefaultValue(0L);
        builder.Property(m => m.TotalMembers).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.MigratedMembers).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.ErrorMessage).HasMaxLength(4000);
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.CompletedAt);
        builder.Property(m => m.DeletedAt);

        // Soft delete query filter applied globally in AppDbContext
    }
}
