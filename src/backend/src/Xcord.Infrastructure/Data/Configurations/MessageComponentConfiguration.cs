using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class MessageComponentConfiguration : IEntityTypeConfiguration<MessageComponent>
{
    public void Configure(EntityTypeBuilder<MessageComponent> builder)
    {
        builder.ToTable("message_components");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.ComponentType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.CustomId).HasMaxLength(100);
        builder.Property(c => c.Label).HasMaxLength(80);
        builder.Property(c => c.OptionsJson).HasColumnType("jsonb");
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.HasOne(c => c.Message).WithMany().HasForeignKey(c => c.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.MessageId);
    }
}
