using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for ForumTag.
/// </summary>
public sealed class ForumTagConfiguration : IEntityTypeConfiguration<ForumTag>
{
    public void Configure(EntityTypeBuilder<ForumTag> builder)
    {
        // Table name
        builder.ToTable("forum_tags");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(ft => ft.Id);
        builder.Property(ft => ft.Id)
            .ValueGeneratedNever();

        // ChannelId (required, FK to Channel with Cascade)
        builder.Property(ft => ft.ChannelId)
            .IsRequired();

        builder.HasOne(ft => ft.Channel)
            .WithMany()
            .HasForeignKey(ft => ft.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ft => ft.ChannelId);

        // Name (required, max 20)
        builder.Property(ft => ft.Name)
            .IsRequired()
            .HasMaxLength(20);

        // EmojiUnicode (optional)
        builder.Property(ft => ft.EmojiUnicode);

        // EmojiId (optional, for custom emoji)
        builder.Property(ft => ft.EmojiId);

        // IsModerated (required, default false)
        builder.Property(ft => ft.IsModerated)
            .IsRequired()
            .HasDefaultValue(false);

        // Position (required)
        builder.Property(ft => ft.Position)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(ft => ft.DeletedAt);

        // Index for ordering: (ChannelId, Position)
        builder.HasIndex(ft => new { ft.ChannelId, ft.Position });

        // Soft delete query filter is applied globally in AppDbContext
    }
}
