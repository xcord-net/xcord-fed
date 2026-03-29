using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Invite.
/// </summary>
public sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        // Table name
        builder.ToTable("invites");

        // Primary key (Code, string)
        builder.HasKey(i => i.Code);
        builder.Property(i => i.Code)
            .IsRequired()
            .HasMaxLength(8);

        // ServerId (FK to Server with Cascade)
        builder.Property(i => i.ServerId)
            .IsRequired();

        builder.HasOne(i => i.Server)
            .WithMany()
            .HasForeignKey(i => i.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // CreatedByUserId (FK to User with SetNull)
        builder.Property(i => i.CreatedByUserId);

        builder.HasOne(i => i.CreatedBy)
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // MaxUses (optional)
        builder.Property(i => i.MaxUses);

        // Uses (default 0)
        builder.Property(i => i.Uses)
            .IsRequired()
            .HasDefaultValue(0);

        // ExpiresAt (optional)
        builder.Property(i => i.ExpiresAt);

        // CreatedAt (required)
        builder.Property(i => i.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(i => i.DeletedAt);

        // GroupId (optional, FK to Group with SetNull)
        builder.Property(i => i.GroupId);

        builder.HasOne(i => i.Group)
            .WithMany()
            .HasForeignKey(i => i.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // ChannelId (optional, FK to Channel with SetNull)
        builder.Property(i => i.ChannelId);

        builder.HasOne(i => i.Channel)
            .WithMany()
            .HasForeignKey(i => i.ChannelId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(i => i.ServerId);
        builder.HasIndex(i => i.ExpiresAt);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
