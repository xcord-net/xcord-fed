using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class ProfileDecorationConfiguration : IEntityTypeConfiguration<ProfileDecoration>
{
    public void Configure(EntityTypeBuilder<ProfileDecoration> builder)
    {
        builder.ToTable("profile_decorations");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.BannerUrl).HasMaxLength(512);
        builder.Property(d => d.BannerColor).HasMaxLength(7);
        builder.Property(d => d.AvatarFrameUrl).HasMaxLength(512);
        builder.Property(d => d.ProfileEffect).HasMaxLength(50);
        builder.Property(d => d.Bio).HasMaxLength(190);
        builder.Property(d => d.Pronouns).HasMaxLength(50);
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.UserId).IsUnique();
    }
}
