namespace Xcord.Entities;

/// <summary>
/// Represents a two-factor authentication code sent via email.
/// Hard-deleted after use.
/// </summary>
public sealed class TwoFactorCode
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// User who owns this 2FA code.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Navigation property to User.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 6-digit 2FA code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Number of failed verification attempts against this code.
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Code expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Code creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
