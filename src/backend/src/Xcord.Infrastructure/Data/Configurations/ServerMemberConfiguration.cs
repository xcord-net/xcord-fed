using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for ServerMember.
/// </summary>
public sealed class ServerMemberConfiguration : IEntityTypeConfiguration<ServerMember>
{
    public void Configure(EntityTypeBuilder<ServerMember> builder)
    {
        // Table name
        builder.ToTable("server_members");

        // Composite primary key (UserId + ServerId)
        builder.HasKey(sm => new { sm.UserId, sm.ServerId });

        // UserId (FK to User with Cascade)
        builder.HasOne(sm => sm.User)
            .WithMany()
            .HasForeignKey(sm => sm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ServerId (FK to Server with Cascade)
        builder.HasOne(sm => sm.Server)
            .WithMany()
            .HasForeignKey(sm => sm.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Nickname (optional, max 32)
        builder.Property(sm => sm.Nickname)
            .HasMaxLength(32);

        // ServerAvatarUrl (optional, max 512)
        builder.Property(sm => sm.ServerAvatarUrl)
            .HasMaxLength(512);

        // JoinedAt (required)
        builder.Property(sm => sm.JoinedAt)
            .IsRequired();

        // FavoriteChannelIds (native bigint[] array)
        builder.Property(sm => sm.FavoriteChannelIds);

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(sm => sm.DeletedAt);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
