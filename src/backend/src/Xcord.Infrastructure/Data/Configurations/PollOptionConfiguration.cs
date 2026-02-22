using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for PollOption.
/// </summary>
public sealed class PollOptionConfiguration : IEntityTypeConfiguration<PollOption>
{
    public void Configure(EntityTypeBuilder<PollOption> builder)
    {
        // Table name
        builder.ToTable("poll_options");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(po => po.Id);
        builder.Property(po => po.Id)
            .ValueGeneratedNever();

        // PollId (required, FK to Poll with Cascade)
        builder.Property(po => po.PollId)
            .IsRequired();

        builder.HasOne(po => po.Poll)
            .WithMany(p => p.Options)
            .HasForeignKey(po => po.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(po => po.PollId);

        // Text (required, max 100)
        builder.Property(po => po.Text)
            .IsRequired()
            .HasMaxLength(100);

        // EmojiUnicode (optional, max 32)
        builder.Property(po => po.EmojiUnicode)
            .IsRequired(false)
            .HasMaxLength(32);

        // VoteCount (required, default 0)
        builder.Property(po => po.VoteCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Position (required)
        builder.Property(po => po.Position)
            .IsRequired();

        // NOTE: No soft delete filter — follows poll lifecycle
    }
}
