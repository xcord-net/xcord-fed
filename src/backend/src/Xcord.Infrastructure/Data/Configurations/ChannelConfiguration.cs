using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Channel.
/// </summary>
public sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        // Table name
        builder.ToTable("channels");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        // ConversationId (required, FK to Conversation with Cascade, Unique)
        builder.Property(c => c.ConversationId)
            .IsRequired();

        builder.HasOne(c => c.Conversation)
            .WithOne()
            .HasForeignKey<Channel>(c => c.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.ConversationId)
            .IsUnique();

        // ServerId (required, FK to Server with Cascade)
        builder.Property(c => c.ServerId)
            .IsRequired();

        builder.HasOne(c => c.Server)
            .WithMany()
            .HasForeignKey(c => c.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // CategoryId (optional, FK to Category with SetNull)
        builder.Property(c => c.CategoryId);

        builder.HasOne(c => c.Category)
            .WithMany(cat => cat.Channels)
            .HasForeignKey(c => c.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // Name (required, max 100)
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Topic (optional, max 1024)
        builder.Property(c => c.Topic)
            .HasMaxLength(1024);

        // Type (required, enum)
        builder.Property(c => c.Type)
            .IsRequired()
            .HasConversion<int>();

        // Position (required)
        builder.Property(c => c.Position)
            .IsRequired();

        // SlowModeSeconds (optional)
        builder.Property(c => c.SlowModeSeconds);

        // IsNsfw (required, default false)
        builder.Property(c => c.IsNsfw)
            .IsRequired()
            .HasDefaultValue(false);

        // DefaultSortOrder (optional, enum for forum channels)
        builder.Property(c => c.DefaultSortOrder)
            .HasConversion<int?>();

        // RequireTag (required, default false, forum channels only)
        builder.Property(c => c.RequireTag)
            .IsRequired()
            .HasDefaultValue(false);

        // DefaultAutoArchiveDuration (optional, forum channels only)
        builder.Property(c => c.DefaultAutoArchiveDuration);

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
