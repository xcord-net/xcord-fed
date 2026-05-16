using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Admin;

public sealed record UpdateBatchPreferenceCommand(bool Enabled);

public sealed record UpdateBatchPreferenceResponse(bool Enabled);

public sealed class UpdateBatchPreferenceHandler(IHubClient hubClient)
    : IRequestHandler<UpdateBatchPreferenceCommand, Result<UpdateBatchPreferenceResponse>>
{
    public async Task<Result<UpdateBatchPreferenceResponse>> Handle(
        UpdateBatchPreferenceCommand request, CancellationToken cancellationToken)
    {
        var success = await hubClient.UpdateBatchPreferenceAsync(request.Enabled, cancellationToken).ConfigureAwait(false);
        if (!success)
            return Error.Failure("HUB_UNAVAILABLE", "Unable to update batch preference on hub");

        return new UpdateBatchPreferenceResponse(request.Enabled);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/admin/system/batch-upgrades", async (
            UpdateBatchPreferenceCommand command,
            UpdateBatchPreferenceHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(command, ct).ConfigureAwait(false);
        })
        .RequireAuthorization(Policies.Admin)
        .Produces<UpdateBatchPreferenceResponse>(200)
        .WithName("UpdateSystemBatchPreference")
        .WithTags("Admin", "System");
    }
}
