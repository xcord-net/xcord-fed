using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Message.
/// </summary>
public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        // Table name
        builder.ToTable("messages");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        // ConversationId (required, FK to Conversation with Cascade)
        builder.Property(m => m.ConversationId)
            .IsRequired();

        builder.HasOne(m => m.Conversation)
            .WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite index on (ConversationId, CreatedAt DESC) - the primary pattern for loading
        // message history (paginated, most-recent-first). CreatedAt is descending because queries
        // always order by newest first. A migration is required to apply this to the database.
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt })
            .IsDescending(false, true);

        // AuthorId (nullable, FK to User with SetNull)
        builder.Property(m => m.AuthorId);

        builder.HasOne(m => m.Author)
            .WithMany()
            .HasForeignKey(m => m.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.AuthorId);

        // Type (required, enum)
        builder.Property(m => m.Type)
            .IsRequired()
            .HasConversion<int>();

        // Content (required, max 4000)
        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(4000);

        // Metadata (stored as text - only serialized/deserialized in C#, no JSONB operators used)
        builder.Property(m => m.Metadata)
            .HasColumnType("text");

        // ReplyToId (nullable, self-reference with SetNull)
        builder.Property(m => m.ReplyToId);

        builder.HasOne(m => m.ReplyTo)
            .WithMany()
            .HasForeignKey(m => m.ReplyToId)
            .OnDelete(DeleteBehavior.SetNull);

        // IsPinned (required, default false)
        builder.Property(m => m.IsPinned)
            .IsRequired()
            .HasDefaultValue(false);

        // PinnedAt (nullable)
        builder.Property(m => m.PinnedAt);

        // EmbedsProcessed (required, default false)
        builder.Property(m => m.EmbedsProcessed)
            .IsRequired()
            .HasDefaultValue(false);

        // EditedAt (nullable)
        builder.Property(m => m.EditedAt);

        // Timestamps
        builder.Property(m => m.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(m => m.DeletedAt);

        // Soft delete query filter is applied globally in AppDbContext
    }
}
