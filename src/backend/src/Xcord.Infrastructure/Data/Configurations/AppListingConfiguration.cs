using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class AppListingConfiguration : IEntityTypeConfiguration<AppListing>
{
    public void Configure(EntityTypeBuilder<AppListing> builder)
    {
        builder.ToTable("app_listings");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Name).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Description).HasMaxLength(4000);
        builder.Property(a => a.ShortDescription).HasMaxLength(200);
        builder.Property(a => a.IconUrl).HasMaxLength(512);
        builder.Property(a => a.Category).HasMaxLength(50);
        builder.Property(a => a.Tags).HasMaxLength(500);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.HasOne(a => a.BotToken).WithMany().HasForeignKey(a => a.BotTokenId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.BotTokenId).IsUnique();
        builder.HasIndex(a => a.IsPublished);
    }
}
