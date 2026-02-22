using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for Conversation.
/// </summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        // Table name
        builder.ToTable("conversations");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        // Type (required, enum)
        builder.Property(c => c.Type)
            .IsRequired()
            .HasConversion<int>();
    }
}
