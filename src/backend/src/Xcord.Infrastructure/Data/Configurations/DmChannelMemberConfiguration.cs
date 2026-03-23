using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for DmChannelMember.
/// </summary>
public sealed class DmChannelMemberConfiguration : IEntityTypeConfiguration<DmChannelMember>
{
    public void Configure(EntityTypeBuilder<DmChannelMember> builder)
    {
        // Table name
        builder.ToTable("dm_channel_members");

        // Composite primary key (UserId + DmChannelId)
        builder.HasKey(dcm => new { dcm.UserId, dcm.DmChannelId });

        // UserId (FK to User with Cascade)
        builder.HasOne(dcm => dcm.User)
            .WithMany()
            .HasForeignKey(dcm => dcm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // DmChannelId (FK to DmChannel with Cascade)
        builder.HasOne(dcm => dcm.DmChannel)
            .WithMany(dc => dc.Members)
            .HasForeignKey(dcm => dcm.DmChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // JoinedAt (required)
        builder.Property(dcm => dcm.JoinedAt)
            .IsRequired();

        // Suppress EF Core warning: required principals DmChannel and User have global query filters
        builder.HasQueryFilter(dcm => dcm.DmChannel!.DeletedAt == null && dcm.User!.DeletedAt == null);
    }
}
