using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for DmChannel.
/// </summary>
public sealed class DmChannelConfiguration : IEntityTypeConfiguration<DmChannel>
{
    public void Configure(EntityTypeBuilder<DmChannel> builder)
    {
        // Table name
        builder.ToTable("dm_channels");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(dc => dc.Id);
        builder.Property(dc => dc.Id)
            .ValueGeneratedNever();

        // ConversationId (FK to Conversation, required, unique)
        builder.HasOne(dc => dc.Conversation)
            .WithMany()
            .HasForeignKey(dc => dc.ConversationId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(dc => dc.ConversationId)
            .IsUnique();

        // OwnerId (FK to User, SetNull)
        builder.HasOne(dc => dc.Owner)
            .WithMany()
            .HasForeignKey(dc => dc.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        // IsGroup (required)
        builder.Property(dc => dc.IsGroup)
            .IsRequired();

        // Name (optional, max 100)
        builder.Property(dc => dc.Name)
            .HasMaxLength(100);

        // DeletedAt (soft delete)
        builder.Property(dc => dc.DeletedAt);
    }
}
