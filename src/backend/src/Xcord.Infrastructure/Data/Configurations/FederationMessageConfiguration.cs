using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for FederationMessage.
/// </summary>
public sealed class FederationMessageConfiguration : IEntityTypeConfiguration<FederationMessage>
{
    public void Configure(EntityTypeBuilder<FederationMessage> builder)
    {
        builder.ToTable("federation_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.FederationFollowId)
            .IsRequired();

        builder.Property(m => m.RemoteMessageId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(m => m.LocalMessageId)
            .IsRequired();

        builder.Property(m => m.RemoteAuthorName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.RemoteAuthorAvatarUrl)
            .HasMaxLength(500);

        builder.Property(m => m.ReceivedAt)
            .IsRequired();

        // Prevent duplicate imports of the same remote message
        builder.HasIndex(m => new { m.FederationFollowId, m.RemoteMessageId })
            .IsUnique();

        builder.HasIndex(m => m.LocalMessageId);

        builder.HasOne(m => m.Follow)
            .WithMany()
            .HasForeignKey(m => m.FederationFollowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.LocalMessage)
            .WithMany()
            .HasForeignKey(m => m.LocalMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Suppress EF Core warning: required principals FederationFollow and Message have global query filters
        builder.HasQueryFilter(m => m.Follow!.DeletedAt == null && m.LocalMessage!.DeletedAt == null);
    }
}
