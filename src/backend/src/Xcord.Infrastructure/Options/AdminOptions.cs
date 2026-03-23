namespace Xcord.Infrastructure.Options;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Pre-hashed password (BCrypt). When set, Password is ignored.
    /// Used by hub provisioning to pass the hash without transmitting plaintext.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
}
