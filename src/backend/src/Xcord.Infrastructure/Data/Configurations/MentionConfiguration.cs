using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Mention.
/// </summary>
public sealed class MentionConfiguration : IEntityTypeConfiguration<Mention>
{
    public void Configure(EntityTypeBuilder<Mention> builder)
    {
        // Table name
        builder.ToTable("mentions");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        // MessageId (required, FK to Message with Cascade)
        builder.Property(m => m.MessageId)
            .IsRequired();

        builder.HasOne(m => m.Message)
            .WithMany(msg => msg.Mentions)
            .HasForeignKey(m => m.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.MessageId);

        // MentionedUserId (nullable, FK to User)
        builder.Property(m => m.MentionedUserId);

        builder.HasOne(m => m.MentionedUser)
            .WithMany()
            .HasForeignKey(m => m.MentionedUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // MentionedGroupId (nullable, FK to Group)
        builder.Property(m => m.MentionedGroupId);

        builder.HasOne(m => m.MentionedGroup)
            .WithMany()
            .HasForeignKey(m => m.MentionedGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // IsEveryone (required, default false)
        builder.Property(m => m.IsEveryone)
            .IsRequired()
            .HasDefaultValue(false);
    }
}
