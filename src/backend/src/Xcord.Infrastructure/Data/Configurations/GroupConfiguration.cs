using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Group.
/// </summary>
public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        // Table name
        builder.ToTable("groups");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id)
            .ValueGeneratedNever();

        // ServerId (required, FK to Server with Cascade)
        builder.Property(g => g.ServerId)
            .IsRequired();

        builder.HasOne(g => g.Server)
            .WithMany(s => s.Groups)
            .HasForeignKey(g => g.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Name (required, max 100)
        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Color (optional, max 7 for hex like "#FFFFFF")
        builder.Property(g => g.Color)
            .HasMaxLength(7);

        // Roles (required, bitfield)
        builder.Property(g => g.Roles)
            .HasColumnName("Roles")
            .IsRequired();

        // Position (required, default 0)
        builder.Property(g => g.Position)
            .IsRequired()
            .HasDefaultValue(0);

        // IsEveryone (required, default false)
        builder.Property(g => g.IsEveryone)
            .IsRequired()
            .HasDefaultValue(false);

        // LimitsJson (optional, jsonb)
        builder.Property(g => g.LimitsJson)
            .HasColumnType("jsonb");

        // Timestamps
        builder.Property(g => g.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(g => g.DeletedAt);

        // Indexes
        builder.HasIndex(g => g.ServerId);
        builder.HasIndex(g => new { g.ServerId, g.Position });
        builder.HasIndex(g => new { g.ServerId, g.IsEveryone })
            .HasFilter("\"IsEveryone\" = true"); // Partial index for finding @everyone group

        // Soft delete query filter is applied globally in AppDbContext
    }
}
