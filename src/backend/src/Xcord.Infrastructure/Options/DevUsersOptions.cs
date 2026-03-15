namespace Xcord.Infrastructure.Options;

public sealed class DevUsersOptions
{
    public const string SectionName = "DevUsers";

    public List<DevUser> Users { get; set; } = [];
}

public sealed class DevUser
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
