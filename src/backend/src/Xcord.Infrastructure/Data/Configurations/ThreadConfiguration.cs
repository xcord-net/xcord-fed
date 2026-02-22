using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreadEntity = Xcord.Entities.Thread;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Thread.
/// </summary>
public sealed class ThreadConfiguration : IEntityTypeConfiguration<ThreadEntity>
{
    public void Configure(EntityTypeBuilder<ThreadEntity> builder)
    {
        // Table name
        builder.ToTable("threads");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        // ConversationId (required, FK to Conversation with Cascade, Unique)
        builder.Property(t => t.ConversationId)
            .IsRequired();

        builder.HasOne(t => t.Conversation)
            .WithOne()
            .HasForeignKey<ThreadEntity>(t => t.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.ConversationId)
            .IsUnique();

        // ChannelId (required, FK to Channel with Cascade)
        builder.Property(t => t.ChannelId)
            .IsRequired();

        builder.HasOne(t => t.Channel)
            .WithMany()
            .HasForeignKey(t => t.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.ChannelId);

        // ParentMessageId (optional, FK to Message with SetNull)
        builder.Property(t => t.ParentMessageId);

        builder.HasOne(t => t.ParentMessage)
            .WithMany()
            .HasForeignKey(t => t.ParentMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        // Title (optional, max 100)
        builder.Property(t => t.Title)
            .HasMaxLength(100);

        // IsArchived (required, default false)
        builder.Property(t => t.IsArchived)
            .IsRequired()
            .HasDefaultValue(false);

        // IsLocked (required, default false)
        builder.Property(t => t.IsLocked)
            .IsRequired()
            .HasDefaultValue(false);

        // AutoArchiveDurationMinutes (required, default 1440)
        builder.Property(t => t.AutoArchiveDurationMinutes)
            .IsRequired()
            .HasDefaultValue(1440);

        // LastActivityAt (required)
        builder.Property(t => t.LastActivityAt)
            .IsRequired();

        // MessageCount (required, default 0)
        builder.Property(t => t.MessageCount)
            .IsRequired()
            .HasDefaultValue(0);

        // CreatedAt (required)
        builder.Property(t => t.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(t => t.DeletedAt);

        // Index for archiver background service: (IsArchived, LastActivityAt)
        builder.HasIndex(t => new { t.IsArchived, t.LastActivityAt });

        // Soft delete query filter is applied globally in AppDbContext
    }
}
