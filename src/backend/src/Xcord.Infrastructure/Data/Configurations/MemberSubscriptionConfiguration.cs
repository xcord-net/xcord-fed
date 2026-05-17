using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class MemberSubscriptionConfiguration : IEntityTypeConfiguration<MemberSubscription>
{
    public void Configure(EntityTypeBuilder<MemberSubscription> builder)
    {
        builder.ToTable("member_subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.StripeSubscriptionId).HasMaxLength(255);
        builder.Property(s => s.StripeCustomerId).HasMaxLength(255);
        builder.Property(s => s.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Server).WithMany().HasForeignKey(s => s.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Tier).WithMany(t => t.Subscriptions).HasForeignKey(s => s.TierId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.UserId, s.ServerId }).HasFilter("\"DeletedAt\" IS NULL");
        builder.HasIndex(s => s.StripeSubscriptionId).HasFilter("\"StripeSubscriptionId\" IS NOT NULL");
        builder.HasIndex(s => s.ServerId);
    }
}
