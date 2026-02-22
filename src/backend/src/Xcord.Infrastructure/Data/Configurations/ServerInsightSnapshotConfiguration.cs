using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class ServerInsightSnapshotConfiguration : IEntityTypeConfiguration<ServerInsightSnapshot>
{
    public void Configure(EntityTypeBuilder<ServerInsightSnapshot> builder)
    {
        builder.ToTable("server_insight_snapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Date).IsRequired();
        builder.Property(s => s.TopChannelIds).HasColumnType("jsonb");
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.HasOne(s => s.Server).WithMany().HasForeignKey(s => s.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.ServerId, s.Date }).IsUnique();
    }
}
