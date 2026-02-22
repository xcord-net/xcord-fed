using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class UserActivityConfiguration : IEntityTypeConfiguration<UserActivity>
{
    public void Configure(EntityTypeBuilder<UserActivity> builder)
    {
        builder.ToTable("user_activities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.ActivityType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(128);
        builder.Property(a => a.Details).HasMaxLength(128);
        builder.Property(a => a.State).HasMaxLength(128);
        builder.Property(a => a.LargeImageUrl).HasMaxLength(512);
        builder.Property(a => a.SmallImageUrl).HasMaxLength(512);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.UserId);
    }
}
