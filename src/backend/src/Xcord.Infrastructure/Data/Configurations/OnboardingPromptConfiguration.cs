using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class OnboardingPromptConfiguration : IEntityTypeConfiguration<OnboardingPrompt>
{
    public void Configure(EntityTypeBuilder<OnboardingPrompt> builder)
    {
        builder.ToTable("onboarding_prompts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Title).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.OptionsJson).HasColumnType("jsonb");
        builder.HasOne(p => p.OnboardingConfig).WithMany(o => o.Prompts).HasForeignKey(p => p.OnboardingConfigId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => p.OnboardingConfigId);
    }
}
