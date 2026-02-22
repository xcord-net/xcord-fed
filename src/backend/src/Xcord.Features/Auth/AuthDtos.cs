namespace Xcord.Features.Auth;

/// <summary>
/// Response DTOs for authentication endpoints.
/// </summary>

public sealed record RegisterResponse(
    long UserId,
    string Username,
    string AccessToken,
    bool EmailConfirmed,
    string RefreshToken,
    string? ConfirmationCode = null
);

public sealed record LoginResponse(
    long UserId,
    string Username,
    string AccessToken,
    bool EmailConfirmed,
    string RefreshToken
);

public sealed record TwoFactorRequiredResponse(
    bool RequiresTwoFactor,
    string TwoFactorToken
);

public sealed record UserInfoResponse(
    long UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    bool IsAdmin,
    bool IsBot
);
