using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Reflection;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Admin;

public sealed record GetSystemVersionQuery;

public sealed record SystemVersionResponse(
    string CurrentVersion,
    bool HubConnected,
    bool BatchUpgradesEnabled,
    List<HubVersionItem>? AvailableVersions,
    List<HubUpgradeHistoryItem>? UpgradeHistory
);

public sealed class GetSystemVersionHandler(IHubClient hubClient)
    : IRequestHandler<GetSystemVersionQuery, Result<SystemVersionResponse>>
{
    public async Task<Result<SystemVersionResponse>> Handle(
        GetSystemVersionQuery request, CancellationToken cancellationToken)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        var currentVersion = version != null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0-dev";

        var versions = await hubClient.GetVersionsAsync(cancellationToken);
        var history = await hubClient.GetUpgradeHistoryAsync(cancellationToken);

        return new SystemVersionResponse(
            currentVersion,
            HubConnected: versions != null,
            BatchUpgradesEnabled: versions?.BatchUpgradesEnabled ?? true,
            AvailableVersions: versions?.Versions,
            UpgradeHistory: history?.Events
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/admin/system/version", async (
            GetSystemVersionHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetSystemVersionQuery(), ct);
        })
        .RequireAuthorization(Policies.Admin)
        .Produces<SystemVersionResponse>(200)
        .WithName("GetSystemVersion")
        .WithTags("Admin", "System");
    }
}
