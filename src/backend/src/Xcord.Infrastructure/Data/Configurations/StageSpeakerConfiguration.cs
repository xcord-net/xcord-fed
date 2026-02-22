using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class StageSpeakerConfiguration : IEntityTypeConfiguration<StageSpeaker>
{
    public void Configure(EntityTypeBuilder<StageSpeaker> builder)
    {
        builder.ToTable("stage_speakers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.HasOne(s => s.StageSession).WithMany(ss => ss.Speakers).HasForeignKey(s => s.StageSessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.StageSessionId, s.UserId }).IsUnique();
    }
}
