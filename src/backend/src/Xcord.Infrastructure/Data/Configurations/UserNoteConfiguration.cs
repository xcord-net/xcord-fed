using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for UserNote.
/// </summary>
public sealed class UserNoteConfiguration : IEntityTypeConfiguration<UserNote>
{
    public void Configure(EntityTypeBuilder<UserNote> builder)
    {
        builder.ToTable("user_notes");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .ValueGeneratedNever();

        builder.Property(n => n.AuthorId)
            .IsRequired();

        builder.Property(n => n.TargetUserId)
            .IsRequired();

        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.Property(n => n.UpdatedAt);

        builder.Property(n => n.DeletedAt);

        // One note per target user per author
        builder.HasIndex(n => new { n.AuthorId, n.TargetUserId })
            .IsUnique();

        builder.HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.TargetUser)
            .WithMany()
            .HasForeignKey(n => n.TargetUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
