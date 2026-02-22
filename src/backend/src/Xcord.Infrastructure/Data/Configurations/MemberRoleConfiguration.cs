using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for MemberRole junction table.
/// </summary>
public sealed class MemberRoleConfiguration : IEntityTypeConfiguration<MemberRole>
{
    public void Configure(EntityTypeBuilder<MemberRole> builder)
    {
        // Table name
        builder.ToTable("member_roles");

        // Composite primary key (UserId + ServerId + RoleId)
        builder.HasKey(mr => new { mr.UserId, mr.ServerId, mr.RoleId });

        // FK to ServerMember (composite FK: UserId + ServerId)
        builder.HasOne(mr => mr.ServerMember)
            .WithMany(sm => sm.MemberRoles)
            .HasForeignKey(mr => new { mr.UserId, mr.ServerId })
            .OnDelete(DeleteBehavior.Cascade);

        // FK to Role (RoleId)
        builder.HasOne(mr => mr.Role)
            .WithMany(r => r.MemberRoles)
            .HasForeignKey(mr => mr.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for querying all members with a specific role
        builder.HasIndex(mr => mr.RoleId);
    }
}
