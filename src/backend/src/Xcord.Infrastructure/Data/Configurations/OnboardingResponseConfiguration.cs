using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class OnboardingCompletionConfiguration : IEntityTypeConfiguration<OnboardingCompletion>
{
    public void Configure(EntityTypeBuilder<OnboardingCompletion> builder)
    {
        builder.ToTable("onboarding_completions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ResponseDataJson).HasColumnType("jsonb");
        builder.Property(r => r.CompletedAt).IsRequired();
        builder.HasOne(r => r.Server).WithMany().HasForeignKey(r => r.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.ServerId, r.UserId }).IsUnique();
    }
}
