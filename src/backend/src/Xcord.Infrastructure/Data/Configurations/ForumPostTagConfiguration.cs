using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for ForumPostTag junction table.
/// </summary>
public sealed class ForumPostTagConfiguration : IEntityTypeConfiguration<ForumPostTag>
{
    public void Configure(EntityTypeBuilder<ForumPostTag> builder)
    {
        // Table name
        builder.ToTable("forum_post_tags");

        // Composite primary key (ThreadId + ForumTagId)
        builder.HasKey(fpt => new { fpt.ThreadId, fpt.ForumTagId });

        // FK to Thread (Cascade delete)
        builder.HasOne(fpt => fpt.Thread)
            .WithMany()
            .HasForeignKey(fpt => fpt.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to ForumTag (Cascade delete)
        builder.HasOne(fpt => fpt.ForumTag)
            .WithMany()
            .HasForeignKey(fpt => fpt.ForumTagId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for querying all posts with a specific tag
        builder.HasIndex(fpt => fpt.ForumTagId);

        // Suppress EF Core warning: required principals Thread and ForumTag have global query filters
        builder.HasQueryFilter(fpt => fpt.Thread!.DeletedAt == null && fpt.ForumTag!.DeletedAt == null);
    }
}
