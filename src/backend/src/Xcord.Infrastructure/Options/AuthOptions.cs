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
}
