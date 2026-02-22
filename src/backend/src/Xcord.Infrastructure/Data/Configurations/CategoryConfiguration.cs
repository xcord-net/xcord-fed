using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Category.
/// </summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        // Table name
        builder.ToTable("categories");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        // ServerId (required, FK to Server with Cascade)
        builder.Property(c => c.ServerId)
            .IsRequired();

        builder.HasOne(c => c.Server)
            .WithMany()
            .HasForeignKey(c => c.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Name (required, max 100)
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Position (required)
        builder.Property(c => c.Position)
            .IsRequired();

        // Timestamps
        builder.Property(c => c.CreatedAt)
            .IsRequired();

        // Soft delete (DeletedAt, implements ISoftDeletable)
        builder.Property(c => c.DeletedAt);

        // Index for ordering: (ServerId, Position)
        builder.HasIndex(c => new { c.ServerId, c.Position });

        // Soft delete query filter is applied globally in AppDbContext
    }
}
