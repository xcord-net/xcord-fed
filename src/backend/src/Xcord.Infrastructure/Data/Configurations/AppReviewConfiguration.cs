using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class AppReviewConfiguration : IEntityTypeConfiguration<AppReview>
{
    public void Configure(EntityTypeBuilder<AppReview> builder)
    {
        builder.ToTable("app_reviews");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Content).HasMaxLength(2000);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.HasOne(r => r.AppListing).WithMany(a => a.Reviews).HasForeignKey(r => r.AppListingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.AppListingId, r.UserId }).IsUnique();
    }
}
