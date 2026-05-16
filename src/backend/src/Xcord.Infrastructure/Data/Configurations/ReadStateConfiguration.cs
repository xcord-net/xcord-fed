using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for ReadState.
/// </summary>
public sealed class ReadStateConfiguration : IEntityTypeConfiguration<ReadState>
{
    public void Configure(EntityTypeBuilder<ReadState> builder)
    {
        // Table name
        builder.ToTable("read_states");

        // Composite primary key (UserId + ConversationId)
        builder.HasKey(rs => new { rs.UserId, rs.ConversationId });

        // UserId (FK to User with Cascade)
        builder.HasOne(rs => rs.User)
            .WithMany()
            .HasForeignKey(rs => rs.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ConversationId (FK to Conversation with Cascade)
        builder.HasOne(rs => rs.Conversation)
            .WithMany()
            .HasForeignKey(rs => rs.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // LastReadMessageId (nullable, FK to Message with SetNull)
        builder.Property(rs => rs.LastReadMessageId);

        builder.HasOne(rs => rs.LastReadMessage)
            .WithMany()
            .HasForeignKey(rs => rs.LastReadMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        // UnreadCount (required, default 0)
        builder.Property(rs => rs.UnreadCount)
            .IsRequired()
            .HasDefaultValue(0);

        // MentionCount (required, default 0)
        builder.Property(rs => rs.MentionCount)
            .IsRequired()
            .HasDefaultValue(0);

        // PostgreSQL xmin system column as the optimistic concurrency token. This
        // surfaces lost-update races on UnreadCount / MentionCount as
        // DbUpdateConcurrencyException, which writers can either retry or sidestep
        // entirely by using an atomic ExecuteUpdateAsync (the preferred path for
        // count increments, since the server applies the delta atomically).
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();

        // Index on UserId for total unread queries
        builder.HasIndex(rs => rs.UserId);

        // Suppress EF Core warning: required principal User has a global query filter
        builder.HasQueryFilter(rs => rs.User!.DeletedAt == null);
    }
}
