using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> builder)
    {
        builder.ToTable("reactions");
        builder.HasKey(r => new { r.MessageId, r.UserId, r.Emoji });
        builder.Property(r => r.MessageId).IsRequired();
        builder.HasOne(r => r.Message).WithMany(m => m.Reactions).HasForeignKey(r => r.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(r => r.UserId).IsRequired();
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(r => r.Emoji).IsRequired().HasMaxLength(32);
        builder.Property(r => r.IsSuper).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.HasIndex(r => r.MessageId);
    }
}
