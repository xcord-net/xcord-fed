using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class OnboardingConfigConfiguration : IEntityTypeConfiguration<OnboardingConfig>
{
    public void Configure(EntityTypeBuilder<OnboardingConfig> builder)
    {
        builder.ToTable("onboarding_configs");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.DefaultChannelIds).HasColumnType("jsonb");
        builder.Property(o => o.RulesText).HasMaxLength(4000);
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.HasOne(o => o.Server).WithMany().HasForeignKey(o => o.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(o => o.ServerId).IsUnique();
    }
}
