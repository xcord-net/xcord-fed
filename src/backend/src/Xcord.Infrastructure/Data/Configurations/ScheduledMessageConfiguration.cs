using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for ScheduledMessage.
/// </summary>
public sealed class ScheduledMessageConfiguration : IEntityTypeConfiguration<ScheduledMessage>
{
    public void Configure(EntityTypeBuilder<ScheduledMessage> builder)
    {
        // Table name
        builder.ToTable("scheduled_messages");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(sm => sm.Id);
        builder.Property(sm => sm.Id)
            .ValueGeneratedNever();

        // ConversationId (required, FK to Conversation with Cascade)
        builder.Property(sm => sm.ConversationId)
            .IsRequired();

        builder.HasOne(sm => sm.Conversation)
            .WithMany()
            .HasForeignKey(sm => sm.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // AuthorId (required, FK to User with Cascade)
        builder.Property(sm => sm.AuthorId)
            .IsRequired();

        builder.HasOne(sm => sm.Author)
            .WithMany()
            .HasForeignKey(sm => sm.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Content (required, max 4000)
        builder.Property(sm => sm.Content)
            .IsRequired()
            .HasMaxLength(4000);

        // ScheduledAt (required)
        builder.Property(sm => sm.ScheduledAt)
            .IsRequired();

        // SentAt (optional)
        builder.Property(sm => sm.SentAt);

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(sm => sm.DeletedAt);

        // Index for listing a user's scheduled messages in a conversation
        builder.HasIndex(sm => new { sm.ConversationId, sm.ScheduledAt });

        // Index for the dispatcher polling query: find due, unsent, non-deleted messages
        builder.HasIndex(sm => new { sm.ScheduledAt, sm.SentAt });

        // Soft delete query filter is applied globally in AppDbContext
    }
}
