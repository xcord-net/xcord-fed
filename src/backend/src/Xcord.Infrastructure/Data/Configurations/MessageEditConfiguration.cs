using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for MessageEdit.
/// </summary>
public sealed class MessageEditConfiguration : IEntityTypeConfiguration<MessageEdit>
{
    public void Configure(EntityTypeBuilder<MessageEdit> builder)
    {
        // Table name
        builder.ToTable("message_edits");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(me => me.Id);
        builder.Property(me => me.Id)
            .ValueGeneratedNever();

        // MessageId (required, FK to Message with Cascade)
        builder.Property(me => me.MessageId)
            .IsRequired();

        builder.HasOne(me => me.Message)
            .WithMany()
            .HasForeignKey(me => me.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(me => me.MessageId);

        // PreviousContent (required)
        builder.Property(me => me.PreviousContent)
            .IsRequired();

        // EditedAt (required)
        builder.Property(me => me.EditedAt)
            .IsRequired();

        // Suppress EF Core warning: required principal Message has a global query filter
        builder.HasQueryFilter(me => me.Message!.DeletedAt == null);
    }
}
