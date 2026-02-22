using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Bots.Commands;

public sealed record ListCommandsQuery(long ServerId);

public sealed class ListCommandsHandler(AppDbContext dbContext)
    : IRequestHandler<ListCommandsQuery, Result<List<SlashCommandResponse>>>
{
    public async Task<Result<List<SlashCommandResponse>>> Handle(ListCommandsQuery request, CancellationToken ct)
    {
        var commands = await dbContext.SlashCommands.AsNoTracking()
            .Where(c => c.ServerId == request.ServerId)
            .OrderBy(c => c.Name)
            .Select(c => new SlashCommandResponse(c.Id, c.ServerId, c.Name, c.Description, c.OptionsJson, c.CreatedAt))
            .ToListAsync(ct);
        return commands;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId}/commands", async (
            long serverId,
            IRequestHandler<ListCommandsQuery, Result<List<SlashCommandResponse>>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ListCommandsQuery(serverId), ct))
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListCommands").WithTags("SlashCommands");
}
