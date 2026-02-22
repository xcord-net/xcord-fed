namespace Xcord.Entities;

/// <summary>
/// Represents an email confirmation token with a 6-digit code.
/// Hard-deleted after use.
/// </summary>
public sealed class EmailConfirmationToken
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// User who owns this confirmation token.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Navigation property to User.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 6-digit confirmation code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Token creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
