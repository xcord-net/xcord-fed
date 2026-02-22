using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class WelcomeScreenConfiguration : IEntityTypeConfiguration<WelcomeScreen>
{
    public void Configure(EntityTypeBuilder<WelcomeScreen> builder)
    {
        builder.ToTable("welcome_screens");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Description).HasMaxLength(1024);
        builder.Property(w => w.CreatedAt).IsRequired();
        builder.HasOne(w => w.Server).WithMany().HasForeignKey(w => w.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(w => w.ServerId).IsUnique();
    }
}
