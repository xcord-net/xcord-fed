using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xcord.Entities;

namespace Xcord.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for EventRsvp.
/// </summary>
public sealed class EventRsvpConfiguration : IEntityTypeConfiguration<EventRsvp>
{
    public void Configure(EntityTypeBuilder<EventRsvp> builder)
    {
        // Table name
        builder.ToTable("event_rsvps");

        // Composite primary key (EventId, UserId)
        builder.HasKey(r => new { r.EventId, r.UserId });

        // EventId (required, FK to ScheduledEvent with Cascade)
        builder.Property(r => r.EventId)
            .IsRequired();

        builder.HasOne(r => r.ScheduledEvent)
            .WithMany()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserId (required, FK to User with Cascade)
        builder.Property(r => r.UserId)
            .IsRequired();

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // CreatedAt (required)
        builder.Property(r => r.CreatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(r => r.UserId);

        // Suppress EF Core warning: required principals ScheduledEvent and User have global query filters
        builder.HasQueryFilter(r => r.ScheduledEvent!.DeletedAt == null && r.User!.DeletedAt == null);
    }
}
