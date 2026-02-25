using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class MemberSubscriptionTierConfiguration : IEntityTypeConfiguration<MemberSubscriptionTier>
{
    public void Configure(EntityTypeBuilder<MemberSubscriptionTier> builder)
    {
        builder.ToTable("member_subscription_tiers");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.PriceMonthly).IsRequired();
        builder.Property(t => t.Currency).IsRequired().HasMaxLength(3);
        builder.Property(t => t.RoleIdsJson).IsRequired().HasColumnType("jsonb");
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.HasOne(t => t.Server).WithMany().HasForeignKey(t => t.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => t.ServerId);
    }
}
