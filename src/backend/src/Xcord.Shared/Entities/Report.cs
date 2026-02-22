using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents a user or message report.
/// </summary>
public sealed class Report : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Server ID (FK to Server).
    /// </summary>
    public long ServerId { get; set; }

    /// <summary>
    /// Reporter user ID (FK to User).
    /// </summary>
    public long ReporterId { get; set; }

    /// <summary>
    /// Reported user ID (FK to User, optional).
    /// </summary>
    public long? ReportedUserId { get; set; }

    /// <summary>
    /// Reported message ID (FK to Message, optional).
    /// </summary>
    public long? ReportedMessageId { get; set; }

    /// <summary>
    /// Reason for the report (max 1000 characters).
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Report status.
    /// </summary>
    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    /// <summary>
    /// Reviewer user ID (FK to User, optional).
    /// </summary>
    public long? ReviewedById { get; set; }

    /// <summary>
    /// Review notes (max 1000 characters).
    /// </summary>
    public string? ReviewNotes { get; set; }

    /// <summary>
    /// Timestamp when the report was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Server Server { get; set; } = null!;
    public User Reporter { get; set; } = null!;
    public User? ReportedUser { get; set; }
    public Message? ReportedMessage { get; set; }
    public User? ReviewedBy { get; set; }
}
