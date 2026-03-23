using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for VoiceState.
/// Composite PK on UserId + ChannelId, NOT soft-deleted.
/// </summary>
public sealed class VoiceStateConfiguration : IEntityTypeConfiguration<VoiceState>
{
    public void Configure(EntityTypeBuilder<VoiceState> builder)
    {
        // Table name
        builder.ToTable("voice_states");

        // Composite primary key (UserId + ChannelId)
        builder.HasKey(vs => new { vs.UserId, vs.ChannelId });

        // FK to User (UserId)
        builder.HasOne(vs => vs.User)
            .WithMany()
            .HasForeignKey(vs => vs.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Channel (ChannelId)
        builder.HasOne(vs => vs.Channel)
            .WithMany()
            .HasForeignKey(vs => vs.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for querying all users in a channel
        builder.HasIndex(vs => vs.ChannelId);

        // JoinedAt timestamp
        builder.Property(vs => vs.JoinedAt)
            .IsRequired();

        // Suppress EF Core warning: required principals User and Channel have global query filters
        builder.HasQueryFilter(vs => vs.User!.DeletedAt == null && vs.Channel!.DeletedAt == null);
    }
}
