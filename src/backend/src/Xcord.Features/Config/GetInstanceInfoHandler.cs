using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Options;

namespace Xcord.Features.Config;

public sealed record GetInstanceInfoResponse(string Name, string? IconUrl);

public sealed class GetInstanceInfoHandler : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/instance/info", (
            [FromServices] IOptions<InstanceOptions> instanceOptions) =>
        {
            return Results.Ok(new GetInstanceInfoResponse(
                instanceOptions.Value.Name,
                null
            ));
        })
        .AllowAnonymous()
        .WithName("GetInstanceInfo")
        .WithTags("Config");
    }
}
