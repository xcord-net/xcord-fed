namespace Xcord.Entities;

/// <summary>
/// Represents a one-time backup code for two-factor authentication recovery.
/// Hard-deleted after use (not soft-deleted).
/// </summary>
public sealed class TwoFactorBackupCode
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// User who owns this backup code.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Navigation property to User.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// BCrypt hash of the backup code (hyphens stripped before hashing).
    /// </summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>
    /// Code creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
