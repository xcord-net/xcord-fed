using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Options;

namespace Xcord.Features.Config;

public sealed record GetConfigResponse(bool RegistrationEnabled, string? HubUrl);

public sealed class GetConfigHandler : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/config", (
            [FromServices] IOptions<AuthOptions> authOptions,
            [FromServices] IOptions<HubOptions> hubOptions) =>
        {
            var hubUrl = hubOptions.Value.Enabled ? hubOptions.Value.Origin : null;
            return Results.Ok(new GetConfigResponse(authOptions.Value.RegistrationEnabled, hubUrl));
        })
        .AllowAnonymous()
        .WithName("GetConfig")
        .WithTags("Config");
    }
}
