using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Options;

namespace Xcord.Features.Config;

public sealed record GetConfigResponse(bool RegistrationEnabled, string? HubUrl, bool CanUseMemberTiers, bool DevLoginEnabled);

public sealed class GetConfigHandler : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/config", (
            [FromServices] IOptions<AuthOptions> authOptions,
            [FromServices] IOptions<HubOptions> hubOptions,
            [FromServices] IOptions<TierOptions> tierOptions,
            [FromServices] IConfiguration configuration) =>
        {
            var hubUrl = hubOptions.Value.Enabled ? hubOptions.Value.Origin : null;
            // Mirrors the gate on TestSeedEndpoint in Program.cs, so the login
            // page only offers the dev login button where the route exists.
            var devLoginEnabled = !string.IsNullOrEmpty(configuration["TestSeed:Key"]);
            return Results.Ok(new GetConfigResponse(
                authOptions.Value.RegistrationEnabled,
                hubUrl,
                tierOptions.Value.CanUseMemberTiers,
                devLoginEnabled));
        })
        .AllowAnonymous()
        .WithName("GetConfig")
        .WithTags("Config");
    }
}
