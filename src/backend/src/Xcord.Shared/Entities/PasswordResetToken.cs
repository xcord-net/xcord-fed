namespace Xcord.Entities;

/// <summary>
/// Represents a password reset token.
/// Hard-deleted after use.
/// </summary>
public sealed class PasswordResetToken
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// SHA-256 hash of the reset token.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// User who owns this reset token.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Navigation property to User.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Token expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Token creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
