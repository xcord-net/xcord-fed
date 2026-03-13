using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for PollVote.
/// </summary>
public sealed class PollVoteConfiguration : IEntityTypeConfiguration<PollVote>
{
    public void Configure(EntityTypeBuilder<PollVote> builder)
    {
        // Table name
        builder.ToTable("poll_votes");

        // Composite primary key (PollOptionId, UserId)
        builder.HasKey(pv => new { pv.PollOptionId, pv.UserId });

        // PollOptionId (required, FK to PollOption with Cascade)
        builder.Property(pv => pv.PollOptionId)
            .IsRequired();

        builder.HasOne(pv => pv.PollOption)
            .WithMany(po => po.Votes)
            .HasForeignKey(pv => pv.PollOptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserId (required, FK to User with Cascade)
        builder.Property(pv => pv.UserId)
            .IsRequired();

        builder.HasOne(pv => pv.User)
            .WithMany()
            .HasForeignKey(pv => pv.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // CreatedAt (required)
        builder.Property(pv => pv.CreatedAt)
            .IsRequired();

        // Index on PollOptionId for efficient lookups
        builder.HasIndex(pv => pv.PollOptionId);

        // Index on UserId for efficient lookups
        builder.HasIndex(pv => pv.UserId);

        // NOTE: No soft delete filter - hard-deleted on retraction
    }
}
