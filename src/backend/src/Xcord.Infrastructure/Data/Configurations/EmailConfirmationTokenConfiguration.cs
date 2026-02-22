using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for EmailConfirmationToken.
/// </summary>
public sealed class EmailConfirmationTokenConfiguration : IEntityTypeConfiguration<EmailConfirmationToken>
{
    public void Configure(EntityTypeBuilder<EmailConfirmationToken> builder)
    {
        // Table name
        builder.ToTable("email_confirmation_tokens");

        // Primary key (Snowflake ID, not auto-generated)
        builder.HasKey(ect => ect.Id);
        builder.Property(ect => ect.Id)
            .ValueGeneratedNever();

        // Code (required, 6 characters)
        builder.Property(ect => ect.Code)
            .IsRequired()
            .HasMaxLength(6);

        // UserId foreign key (Cascade delete)
        builder.HasOne(ect => ect.User)
            .WithMany()
            .HasForeignKey(ect => ect.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on UserId for fast lookups
        builder.HasIndex(ect => ect.UserId);

        // ExpiresAt (required)
        builder.Property(ect => ect.ExpiresAt)
            .IsRequired();

        // Index on ExpiresAt for cleanup queries
        builder.HasIndex(ect => ect.ExpiresAt);

        // CreatedAt (required)
        builder.Property(ect => ect.CreatedAt)
            .IsRequired();
    }
}
