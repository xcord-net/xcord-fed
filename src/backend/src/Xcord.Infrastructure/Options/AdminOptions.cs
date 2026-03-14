namespace Xcord.Infrastructure.Options;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
