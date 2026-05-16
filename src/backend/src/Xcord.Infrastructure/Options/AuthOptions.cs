using System.ComponentModel.DataAnnotations;

namespace Xcord.Infrastructure.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Range(4, 31)]
    public int BcryptWorkFactor { get; set; } = 12;

    /// <summary>
    /// Whether public registration is enabled. When false, new users can only
    /// join via invites from existing members. Defaults to true for standalone
    /// instances; hub-provisioned instances set this to false.
    /// </summary>
    public bool RegistrationEnabled { get; set; } = true;

    /// <summary>
    /// JWT access-token lifetime in minutes. Authoritative knob — also used to
    /// set the access-token cookie Max-Age so the cookie expires with the JWT.
    /// </summary>
    [Range(1, 1440)]
    public int JwtAccessTokenMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh-token lifetime in days. The refresh-token cookie Max-Age tracks
    /// this value.
    /// </summary>
    [Range(1, 365)]
    public int JwtRefreshTokenDays { get; set; } = 30;

    /// <summary>
    /// Maximum failed login attempts permitted within
    /// <see cref="LoginAttemptWindowMinutes"/> before the account locks out.
    /// </summary>
    [Range(1, 100)]
    public int MaxLoginAttemptsPerWindow { get; set; } = 5;

    /// <summary>
    /// Rolling window in minutes for counting failed login attempts.
    /// Also doubles as the lockout duration once the cap is hit.
    /// </summary>
    [Range(1, 1440)]
    public int LoginAttemptWindowMinutes { get; set; } = 15;
}
