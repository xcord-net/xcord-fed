using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class ServerBoostConfiguration : IEntityTypeConfiguration<ServerBoost>
{
    public void Configure(EntityTypeBuilder<ServerBoost> builder)
    {
        builder.ToTable("server_boosts");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.StartedAt).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.HasOne(b => b.Server).WithMany().HasForeignKey(b => b.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(b => b.User).WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(b => new { b.ServerId, b.UserId }).IsUnique();
        builder.HasIndex(b => b.ServerId);
    }
}
