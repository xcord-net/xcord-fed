using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Poll.
/// </summary>
public sealed class PollConfiguration : IEntityTypeConfiguration<Poll>
{
    public void Configure(EntityTypeBuilder<Poll> builder)
    {
        // Table name
        builder.ToTable("polls");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        // MessageId (required, unique FK to Message with Cascade)
        builder.Property(p => p.MessageId)
            .IsRequired();

        builder.HasOne(p => p.Message)
            .WithOne()
            .HasForeignKey<Poll>(p => p.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.MessageId)
            .IsUnique();

        // Question (required, max 300)
        builder.Property(p => p.Question)
            .IsRequired()
            .HasMaxLength(300);

        // AllowMultipleAnswers (required, default false)
        builder.Property(p => p.AllowMultipleAnswers)
            .IsRequired()
            .HasDefaultValue(false);

        // ExpiresAt (optional)
        builder.Property(p => p.ExpiresAt)
            .IsRequired(false);

        // IsClosed (required, default false)
        builder.Property(p => p.IsClosed)
            .IsRequired()
            .HasDefaultValue(false);

        // DeletedAt (soft delete, optional)
        builder.Property(p => p.DeletedAt)
            .IsRequired(false);

        // Index on IsClosed and ExpiresAt for background service query
        builder.HasIndex(p => new { p.IsClosed, p.ExpiresAt });
    }
}
