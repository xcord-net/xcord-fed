using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Servers.Templates;

public sealed record ListServerTemplatesQuery;

public sealed record ServerTemplateResponse(
    long Id, string Name, string? Description, long? SourceServerId,
    string ChannelData, string RoleData, int UsageCount, DateTimeOffset CreatedAt);

public sealed class ListServerTemplatesHandler(AppDbContext dbContext)
    : IRequestHandler<ListServerTemplatesQuery, Result<List<ServerTemplateResponse>>>
{
    public async Task<Result<List<ServerTemplateResponse>>> Handle(ListServerTemplatesQuery request, CancellationToken ct)
    {
        var templates = await dbContext.ServerTemplates
            .AsNoTracking()
            .OrderByDescending(t => t.UsageCount)
            .Take(50)
            .Select(t => new ServerTemplateResponse(t.Id, t.Name, t.Description, t.SourceServerId,
                t.ChannelData, t.RoleData, t.UsageCount, t.CreatedAt))
            .ToListAsync(ct);

        return templates;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/server-templates", async (
            IRequestHandler<ListServerTemplatesQuery, Result<List<ServerTemplateResponse>>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ListServerTemplatesQuery(), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ListServerTemplates").WithTags("ServerTemplates");
}
