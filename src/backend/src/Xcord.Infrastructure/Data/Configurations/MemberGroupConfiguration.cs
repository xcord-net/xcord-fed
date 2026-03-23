using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for MemberGroup junction table.
/// </summary>
public sealed class MemberGroupConfiguration : IEntityTypeConfiguration<MemberGroup>
{
    public void Configure(EntityTypeBuilder<MemberGroup> builder)
    {
        // Table name
        builder.ToTable("member_groups");

        // Composite primary key (UserId + ServerId + GroupId)
        builder.HasKey(mg => new { mg.UserId, mg.ServerId, mg.GroupId });

        // FK to ServerMember (composite FK: UserId + ServerId)
        builder.HasOne(mg => mg.ServerMember)
            .WithMany(sm => sm.MemberGroups)
            .HasForeignKey(mg => new { mg.UserId, mg.ServerId })
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Group (GroupId)
        builder.HasOne(mg => mg.Group)
            .WithMany(g => g.MemberGroups)
            .HasForeignKey(mg => mg.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for querying all members with a specific group
        builder.HasIndex(mg => mg.GroupId);

        // Suppress EF Core warning: required principals ServerMember and Group have global query filters
        builder.HasQueryFilter(mg => mg.ServerMember!.DeletedAt == null && mg.Group!.DeletedAt == null);
    }
}
