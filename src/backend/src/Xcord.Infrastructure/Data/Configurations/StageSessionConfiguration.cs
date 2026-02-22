using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class StageSessionConfiguration : IEntityTypeConfiguration<StageSession>
{
    public void Configure(EntityTypeBuilder<StageSession> builder)
    {
        builder.ToTable("stage_sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Topic).HasMaxLength(120);
        builder.Property(s => s.StartedAt).IsRequired();
        builder.HasOne(s => s.Channel).WithMany().HasForeignKey(s => s.ChannelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.ChannelId);
    }
}
