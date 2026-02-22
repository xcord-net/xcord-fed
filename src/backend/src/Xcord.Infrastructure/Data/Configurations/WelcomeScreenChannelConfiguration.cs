using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class WelcomeScreenChannelConfiguration : IEntityTypeConfiguration<WelcomeScreenChannel>
{
    public void Configure(EntityTypeBuilder<WelcomeScreenChannel> builder)
    {
        builder.ToTable("welcome_screen_channels");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Description).HasMaxLength(200);
        builder.Property(c => c.EmojiName).HasMaxLength(50);
        builder.HasOne(c => c.WelcomeScreen).WithMany(w => w.Channels).HasForeignKey(c => c.WelcomeScreenId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Channel).WithMany().HasForeignKey(c => c.ChannelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.WelcomeScreenId);
    }
}
