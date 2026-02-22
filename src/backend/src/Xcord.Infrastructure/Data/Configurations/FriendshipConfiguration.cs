using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Friendship.
/// </summary>
public sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        // Table name
        builder.ToTable("friendships");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .ValueGeneratedNever();

        // SenderId (required)
        builder.Property(f => f.SenderId)
            .IsRequired();

        // ReceiverId (required)
        builder.Property(f => f.ReceiverId)
            .IsRequired();

        // Status (required)
        builder.Property(f => f.Status)
            .IsRequired();

        // CreatedAt (required)
        builder.Property(f => f.CreatedAt)
            .IsRequired();

        // DeletedAt (soft delete)
        builder.Property(f => f.DeletedAt);

        // Foreign keys
        builder.HasOne(f => f.Sender)
            .WithMany()
            .HasForeignKey(f => f.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Receiver)
            .WithMany()
            .HasForeignKey(f => f.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique index on (SenderId, ReceiverId) to prevent duplicate friend requests
        builder.HasIndex(f => new { f.SenderId, f.ReceiverId })
            .IsUnique();

        // Soft delete query filter is applied globally in AppDbContext
    }
}
