using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Xcord;

namespace Xcord.Features.Authorization;

public static class AuthorizationExtensions
{
    public static TBuilder RequireAnyAuthorization<TBuilder>(
        this TBuilder builder, params string[] policyNames)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireAuthorization(policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context =>
                policyNames.Any(name => EvaluatePolicy(context.User, name)));
        });
    }

    private static bool EvaluatePolicy(ClaimsPrincipal user, string policy) => policy switch
    {
        Policies.User => IsUser(user),
        Policies.Bot => user.HasClaim("bot", "true"),
        Policies.Admin => IsUser(user) && user.HasClaim("admin", "true"),
        _ => false
    };

    // Tiered: Authenticated -> EmailConfirmed -> User -> Admin/Bot
    private static bool IsEmailConfirmed(ClaimsPrincipal user) =>
        user.HasClaim("email_confirmed", "true");

    private static bool IsUser(ClaimsPrincipal user) =>
        IsEmailConfirmed(user) && !user.HasClaim("bot", "true");
}
