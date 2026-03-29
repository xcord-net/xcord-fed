namespace Xcord.Entities;

/// <summary>
/// Represents a bot authentication token.
/// Bot tokens are SHA-256 hashed and can be revoked but not deleted.
/// </summary>
public sealed class BotToken : ISoftDeletable
{
    /// <summary>
    /// Unique Snowflake identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// SHA-256 hash of the raw token.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Bot user account that this token belongs to.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Navigation property to User.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Display name for this token (max 100 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Role bitfield cap - limits what roles the bot can use.
    /// </summary>
    public long Roles { get; set; }

    /// <summary>
    /// Whether this token has been revoked.
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// Token creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last time this token was used for authentication.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Webhook URL to POST interaction events to (slash commands, button clicks, select menus).
    /// Null means the bot does not use webhook delivery (it polls instead).
    /// Max 2048 characters.
    /// </summary>
    public string? InteractionEndpointUrl { get; set; }

    /// <summary>
    /// HMAC-SHA256 signing key for interaction payloads, encrypted at rest via IEncryptionService.
    /// Null when InteractionEndpointUrl is not configured.
    /// </summary>
    public byte[]? InteractionSigningKey { get; set; }

    /// <summary>
    /// References a bundled bot agent ID from the BotAgentRegistry.
    /// Null means this is a custom/external bot (not a bundled agent).
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// JSON parameter values configured by the admin for the bundled agent.
    /// Null when AgentId is null.
    /// </summary>
    public string? AgentConfigJson { get; set; }

    /// <summary>
    /// Soft delete timestamp (implements ISoftDeletable).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
