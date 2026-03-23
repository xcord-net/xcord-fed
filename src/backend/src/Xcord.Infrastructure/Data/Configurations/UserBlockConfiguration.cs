using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for UserBlock.
/// </summary>
public sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        // Table name
        builder.ToTable("user_blocks");

        // Composite primary key
        builder.HasKey(ub => new { ub.BlockerId, ub.BlockedId });

        // BlockerId (required)
        builder.Property(ub => ub.BlockerId)
            .IsRequired();

        // BlockedId (required)
        builder.Property(ub => ub.BlockedId)
            .IsRequired();

        // CreatedAt (required)
        builder.Property(ub => ub.CreatedAt)
            .IsRequired();

        // Foreign keys with cascade delete
        builder.HasOne(ub => ub.Blocker)
            .WithMany()
            .HasForeignKey(ub => ub.BlockerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ub => ub.Blocked)
            .WithMany()
            .HasForeignKey(ub => ub.BlockedId)
            .OnDelete(DeleteBehavior.Cascade);

        // Suppress EF Core warning: required principals Blocker and Blocked (both User) have global query filters
        builder.HasQueryFilter(ub => ub.Blocker!.DeletedAt == null && ub.Blocked!.DeletedAt == null);
    }
}
