using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for BroadcastStageSlot.
/// </summary>
public sealed class BroadcastStageSlotConfiguration : IEntityTypeConfiguration<BroadcastStageSlot>
{
    public void Configure(EntityTypeBuilder<BroadcastStageSlot> builder)
    {
        builder.ToTable("broadcast_stage_slots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.BroadcastId).IsRequired();
        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.SlotIndex).IsRequired();
        builder.Property(s => s.AddedAt).IsRequired();

        builder.HasOne(s => s.Broadcast)
            .WithMany(b => b.StageSlots)
            .HasForeignKey(s => s.BroadcastId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.BroadcastId, s.UserId }).IsUnique();
        builder.HasIndex(s => new { s.BroadcastId, s.SlotIndex }).IsUnique();
    }
}
