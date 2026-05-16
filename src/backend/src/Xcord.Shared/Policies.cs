namespace Xcord;

public static class Policies
{
    public const string User = "User";
    public const string Bot = "Bot";
    public const string Admin = "Admin";

    /// <summary>
    /// Internal-only endpoints authenticated via the X-Internal-Key shared secret.
    /// Used by xcord-hub to call into the instance for stats, shutdown, etc.
    /// </summary>
    public const string InternalKey = "InternalKey";
}
