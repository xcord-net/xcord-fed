using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class OutgoingWebhookConfiguration : IEntityTypeConfiguration<OutgoingWebhook>
{
    public void Configure(EntityTypeBuilder<OutgoingWebhook> builder)
    {
        builder.ToTable("outgoing_webhooks");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Url).IsRequired().HasMaxLength(2048);
        builder.Property(w => w.Secret).IsRequired().HasMaxLength(256);
        builder.Property(w => w.EventTypes).IsRequired().HasColumnType("jsonb");
        builder.Property(w => w.CreatedAt).IsRequired();
        builder.HasOne(w => w.Server).WithMany().HasForeignKey(w => w.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(w => w.ServerId);
    }
}
