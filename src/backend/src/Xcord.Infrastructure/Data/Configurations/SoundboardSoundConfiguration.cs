using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class SoundboardSoundConfiguration : IEntityTypeConfiguration<SoundboardSound>
{
    public void Configure(EntityTypeBuilder<SoundboardSound> builder)
    {
        builder.ToTable("soundboard_sounds");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(80);
        builder.Property(s => s.S3Key).IsRequired().HasMaxLength(512);
        builder.Property(s => s.AudioUrl).IsRequired().HasMaxLength(512);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.HasOne(s => s.Server).WithMany().HasForeignKey(s => s.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.UploadedByUser).WithMany().HasForeignKey(s => s.UploadedByUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.ServerId);
    }
}
