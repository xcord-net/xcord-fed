using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for AuditLog.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        // Table name
        builder.ToTable("audit_logs");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        // Foreign keys
        builder.Property(a => a.ServerId)
            .IsRequired();

        builder.Property(a => a.ActorId);

        // ActionType (required, max 50)
        builder.Property(a => a.ActionType)
            .IsRequired()
            .HasMaxLength(50);

        // TargetId (optional)
        builder.Property(a => a.TargetId);

        // Changes (optional, jsonb)
        builder.Property(a => a.Changes)
            .HasColumnType("jsonb");

        // Reason (optional, max 512)
        builder.Property(a => a.Reason)
            .HasMaxLength(512);

        // CreatedAt (required)
        builder.Property(a => a.CreatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(a => a.ServerId);
        builder.HasIndex(a => a.ActorId);
        builder.HasIndex(a => a.ActionType);
        builder.HasIndex(a => a.CreatedAt);

        // Navigation properties
        builder.HasOne(a => a.Server)
            .WithMany()
            .HasForeignKey(a => a.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Actor)
            .WithMany()
            .HasForeignKey(a => a.ActorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
