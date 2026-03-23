using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for ThreadMember.
/// </summary>
public sealed class ThreadMemberConfiguration : IEntityTypeConfiguration<ThreadMember>
{
    public void Configure(EntityTypeBuilder<ThreadMember> builder)
    {
        // Table name
        builder.ToTable("thread_members");

        // Composite primary key (UserId + ThreadId)
        builder.HasKey(tm => new { tm.UserId, tm.ThreadId });

        // UserId (FK to User with Cascade)
        builder.HasOne(tm => tm.User)
            .WithMany()
            .HasForeignKey(tm => tm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ThreadId (FK to Thread with Cascade)
        builder.HasOne(tm => tm.Thread)
            .WithMany(t => t.ThreadMembers)
            .HasForeignKey(tm => tm.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        // JoinedAt (required)
        builder.Property(tm => tm.JoinedAt)
            .IsRequired();

        // Suppress EF Core warning: required principals User and Thread have global query filters
        builder.HasQueryFilter(tm => tm.User!.DeletedAt == null && tm.Thread!.DeletedAt == null);
    }
}
