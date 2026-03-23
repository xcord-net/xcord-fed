using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Call.
/// NOT soft-deleted - completed calls remain as records.
/// </summary>
public sealed class CallConfiguration : IEntityTypeConfiguration<Call>
{
    public void Configure(EntityTypeBuilder<Call> builder)
    {
        // Table name
        builder.ToTable("calls");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        // FK to DmChannel
        builder.HasOne(c => c.DmChannel)
            .WithMany()
            .HasForeignKey(c => c.DmChannelId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        // FK to User (Caller)
        builder.HasOne(c => c.Caller)
            .WithMany()
            .HasForeignKey(c => c.CallerId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        // Status (required)
        builder.Property(c => c.Status)
            .IsRequired();

        // StartedAt (required)
        builder.Property(c => c.StartedAt)
            .IsRequired();

        // AnsweredAt (optional)
        builder.Property(c => c.AnsweredAt);

        // EndedAt (optional)
        builder.Property(c => c.EndedAt);

        // Index on DmChannelId for querying calls by DM
        builder.HasIndex(c => c.DmChannelId);

        // Index on Status for efficient filtering (e.g., finding ringing calls)
        builder.HasIndex(c => c.Status);

        // Composite index for finding active/ringing calls by DM
        builder.HasIndex(c => new { c.DmChannelId, c.Status });

        // Suppress EF Core warning: required principals DmChannel and Caller have global query filters
        builder.HasQueryFilter(c => c.DmChannel!.DeletedAt == null && c.Caller!.DeletedAt == null);
    }
}
