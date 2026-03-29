using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class DiscordIdMappingConfiguration : IEntityTypeConfiguration<DiscordIdMapping>
{
    public void Configure(EntityTypeBuilder<DiscordIdMapping> builder)
    {
        builder.ToTable("discord_id_mappings");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.MigrationId).IsRequired();
        builder.Property(m => m.DiscordId).IsRequired().HasMaxLength(32);
        builder.Property(m => m.XcordId).IsRequired();
        builder.Property(m => m.EntityType).IsRequired().HasMaxLength(20);

        builder.HasQueryFilter(m => m.Migration.DeletedAt == null);

        builder.HasOne(m => m.Migration)
            .WithMany()
            .HasForeignKey(m => m.MigrationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.MigrationId, m.DiscordId, m.EntityType });
        builder.HasIndex(m => new { m.MigrationId, m.EntityType });
    }
}
