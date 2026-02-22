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
        builder.ToTable("scheduled_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.ConversationId)
            .IsRequired();

        builder.Property(m => m.AuthorId)
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(m => m.Metadata)
            .HasColumnType("jsonb");

        builder.Property(m => m.ScheduledAt)
            .IsRequired();

        builder.Property(m => m.IsSent)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(m => m.SentAt);

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.DeletedAt);

        // Index for background service to find pending messages
        builder.HasIndex(m => new { m.IsSent, m.ScheduledAt });

        builder.HasIndex(m => new { m.ConversationId, m.AuthorId });

        builder.HasOne(m => m.Conversation)
            .WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Author)
            .WithMany()
            .HasForeignKey(m => m.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
