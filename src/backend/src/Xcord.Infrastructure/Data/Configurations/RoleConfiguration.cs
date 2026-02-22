using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Role.
/// </summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // Table name
        builder.ToTable("roles");

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

        // Name (required, max 100)
        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Color (optional, max 7 for hex like "#FFFFFF")
        builder.Property(r => r.Color)
            .HasMaxLength(7);

        // Permissions (required, bitfield)
        builder.Property(r => r.Permissions)
            .IsRequired();

        // Position (required, default 0)
        builder.Property(r => r.Position)
            .IsRequired()
            .HasDefaultValue(0);

        // IsEveryone (required, default false)
        builder.Property(r => r.IsEveryone)
            .IsRequired()
            .HasDefaultValue(false);

        // Timestamps
        builder.Property(r => r.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(r => r.DeletedAt);

        // Indexes
        builder.HasIndex(r => r.ServerId);
        builder.HasIndex(r => new { r.ServerId, r.Position });
        builder.HasIndex(r => new { r.ServerId, r.IsEveryone })
            .HasFilter("\"IsEveryone\" = true"); // Partial index for finding @everyone role

        // Soft delete query filter is applied globally in AppDbContext
    }
}
