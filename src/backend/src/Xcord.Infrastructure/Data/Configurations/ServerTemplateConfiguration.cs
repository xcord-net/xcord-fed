using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class ServerTemplateConfiguration : IEntityTypeConfiguration<ServerTemplate>
{
    public void Configure(EntityTypeBuilder<ServerTemplate> builder)
    {
        builder.ToTable("server_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Description).HasMaxLength(1024);
        builder.Property(t => t.ChannelData).IsRequired().HasColumnType("jsonb");
        builder.Property(t => t.RoleData).IsRequired().HasColumnType("jsonb");
        builder.Property(t => t.UsageCount).HasDefaultValue(0);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.HasOne(t => t.SourceServer).WithMany().HasForeignKey(t => t.SourceServerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(t => t.SourceServerId);
    }
}
