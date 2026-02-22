using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class ConnectedAccountConfiguration : IEntityTypeConfiguration<ConnectedAccount>
{
    public void Configure(EntityTypeBuilder<ConnectedAccount> builder)
    {
        builder.ToTable("connected_accounts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Provider).IsRequired().HasMaxLength(50);
        builder.Property(c => c.ProviderAccountId).IsRequired().HasMaxLength(256);
        builder.Property(c => c.ProviderUsername).HasMaxLength(256);
        builder.Property(c => c.AccessToken).HasMaxLength(4000);
        builder.Property(c => c.RefreshToken).HasMaxLength(4000);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.UserId, c.Provider }).IsUnique();
    }
}
