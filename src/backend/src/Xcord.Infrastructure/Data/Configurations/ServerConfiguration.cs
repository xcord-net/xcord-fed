using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class ServerConfiguration : IEntityTypeConfiguration<Server>
{
    public void Configure(EntityTypeBuilder<Server> builder)
    {
        builder.ToTable("servers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Description).HasMaxLength(1024);
        builder.Property(s => s.IconUrl).HasMaxLength(512);
        builder.Property(s => s.BannerUrl).HasMaxLength(512);
        builder.Property(s => s.OwnerId).IsRequired();
        builder.HasOne(s => s.Owner).WithMany().HasForeignKey(s => s.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(s => s.MemberCount).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.PreferredLocale).HasMaxLength(10);
        builder.Property(s => s.VanitySlug).HasMaxLength(32);
        builder.HasIndex(s => s.VanitySlug).IsUnique().HasFilter("\"VanitySlug\" IS NOT NULL");
        builder.Property(s => s.BoostLevel).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.BoostCount).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.SystemChannelId);
        builder.HasOne<Channel>().WithMany().HasForeignKey("SystemChannelId").OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(s => s.SystemChannelId);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.DeletedAt);
    }
}
