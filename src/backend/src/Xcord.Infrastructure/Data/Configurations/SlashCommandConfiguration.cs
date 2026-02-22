using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

public sealed class SlashCommandConfiguration : IEntityTypeConfiguration<SlashCommand>
{
    public void Configure(EntityTypeBuilder<SlashCommand> builder)
    {
        builder.ToTable("slash_commands");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(32);
        builder.Property(c => c.Description).HasMaxLength(100);
        builder.Property(c => c.OptionsJson).HasColumnType("jsonb");
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.HasOne(c => c.BotToken).WithMany().HasForeignKey(c => c.BotTokenId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Server).WithMany().HasForeignKey(c => c.ServerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.ServerId, c.Name }).IsUnique();
    }
}
