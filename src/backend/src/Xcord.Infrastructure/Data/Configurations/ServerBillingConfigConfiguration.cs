using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class ServerBillingConfigConfiguration : IEntityTypeConfiguration<ServerBillingConfig>
{
    public void Configure(EntityTypeBuilder<ServerBillingConfig> builder)
    {
        builder.ToTable("server_billing_configs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.StripeConnectedAccountId).HasMaxLength(255);
        builder.Property(c => c.RevenueSharePercent).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.HasOne(c => c.Server).WithMany().HasForeignKey(c => c.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.ServerId).IsUnique();
    }
}
