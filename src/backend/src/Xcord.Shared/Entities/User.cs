using Xcord.Entities;

namespace Xcord.Entities;

/// <summary>
/// Represents a user account in the instance.
/// Emails are encrypted via pgcrypto and indexed with HMAC-SHA256 blind index.
/// </summary>
public sealed class User : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Unique username (max 32 characters).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown to other users (max 32 characters).
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted email address (stored as bytea, encrypted via pgcrypto).
    /// </summary>
    public byte[] Email { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// HMAC-SHA256 blind index for email lookup (unique constraint).
    /// </summary>
    public byte[] EmailHash { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// BCrypt password hash (work factor 12, max 128 characters).
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Avatar URL (max 512 characters).
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// User bio/description (max 190 characters).
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Current presence status.
    /// </summary>
    public PresenceStatus Status { get; set; } = PresenceStatus.Offline;

    /// <summary>
    /// Custom status message (max 128 characters).
    /// </summary>
    public string? CustomStatus { get; set; }

    /// <summary>
    /// Whether this is a bot account.
    /// </summary>
    public bool IsBot { get; set; } = false;

    /// <summary>
    /// Whether this user has admin privileges.
    /// </summary>
    public bool IsAdmin { get; set; } = false;

    /// <summary>
    /// Whether this account is disabled.
    /// </summary>
    public bool IsDisabled { get; set; } = false;

    /// <summary>
    /// Whether the email address has been confirmed.
    /// </summary>
    public bool EmailConfirmed { get; set; } = false;

    /// <summary>
    /// Whether two-factor authentication is enabled.
    /// </summary>
    public bool TwoFactorEnabled { get; set; } = false;

    /// <summary>
    /// Cumulative count of failed 2FA verification attempts.
    /// Account is locked after 10 failed attempts. Reset on successful verification.
    /// </summary>
    public int TwoFactorFailureCount { get; set; }

    /// <summary>
    /// Timestamp when the account was locked due to too many failed 2FA attempts.
    /// </summary>
    public DateTimeOffset? TwoFactorLockedAt { get; set; }

    /// <summary>
    /// Account creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last login timestamp.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Scheduled deletion timestamp — set when user requests account deletion.
    /// Actual deletion occurs 14 days after this timestamp.
    /// </summary>
    public DateTimeOffset? ScheduledDeletionAt { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
