using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Admin;

public sealed record RequestUpgradeCommand(string TargetVersion);

public sealed class RequestUpgradeHandler(IHubClient hubClient)
    : IRequestHandler<RequestUpgradeCommand, Result<bool>>, IValidatable<RequestUpgradeCommand>
{
    public Error? Validate(RequestUpgradeCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetVersion))
            return Error.Validation("VALIDATION_FAILED", "TargetVersion is required");
        return null;
    }

    public async Task<Result<bool>> Handle(
        RequestUpgradeCommand request, CancellationToken cancellationToken)
    {
        var success = await hubClient.RequestUpgradeAsync(request.TargetVersion, cancellationToken).ConfigureAwait(false);
        if (!success)
            return Error.Failure("HUB_UNAVAILABLE", "Unable to request upgrade from hub");

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/admin/system/upgrade", async (
            RequestUpgradeCommand command,
            RequestUpgradeHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(command, ct,
                success => Results.Accepted());
        })
        .RequireAuthorization(Policies.Admin)
        .Produces(202)
        .WithName("RequestSystemUpgrade")
        .WithTags("Admin", "System");
    }
}
