using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class OutgoingWebhookDeliveryConfiguration : IEntityTypeConfiguration<OutgoingWebhookDelivery>
{
    public void Configure(EntityTypeBuilder<OutgoingWebhookDelivery> builder)
    {
        builder.ToTable("outgoing_webhook_deliveries");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.EventType).IsRequired().HasMaxLength(50);
        builder.Property(d => d.Payload).HasColumnType("jsonb");
        builder.Property(d => d.ResponseBody).HasMaxLength(4000);
        builder.Property(d => d.DeliveredAt).IsRequired();
        builder.HasOne(d => d.OutgoingWebhook).WithMany(w => w.Deliveries).HasForeignKey(d => d.OutgoingWebhookId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.OutgoingWebhookId);
    }
}
