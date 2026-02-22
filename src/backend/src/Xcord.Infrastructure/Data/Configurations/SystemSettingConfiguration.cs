using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for SystemSetting.
/// </summary>
public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        // Table name
        builder.ToTable("system_settings");

        // Primary key is Key (string)
        builder.HasKey(ss => ss.Key);
        builder.Property(ss => ss.Key)
            .IsRequired()
            .HasMaxLength(100);

        // Value (required, max 8000 for RSA keys)
        builder.Property(ss => ss.Value)
            .IsRequired()
            .HasMaxLength(8000);

        // Timestamps
        builder.Property(ss => ss.CreatedAt)
            .IsRequired();

        builder.Property(ss => ss.UpdatedAt)
            .IsRequired();
    }
}
