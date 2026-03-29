using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Xcord.Infrastructure.Services.Bots;

namespace Xcord.Features.Admin;

public sealed record ListAgentsQuery;

public sealed record AgentParameterDto(
    string Key,
    string Label,
    string Type
);

public sealed record AgentManifestDto(
    string Id,
    string Name,
    string Description,
    string Category,
    AgentParameterDto[] Parameters
);

public sealed record ListAgentsResponse(
    AgentManifestDto[] Agents
);

public sealed class ListAgentsHandler(
    BotAgentRegistry agentRegistry)
    : IRequestHandler<ListAgentsQuery, Result<ListAgentsResponse>>
{
    public Task<Result<ListAgentsResponse>> Handle(ListAgentsQuery request, CancellationToken cancellationToken)
    {
        var agents = agentRegistry.GetAll()
            .Select(m => new AgentManifestDto(
                m.Id,
                m.Name,
                m.Description,
                m.Category,
                m.Parameters.Select(p => new AgentParameterDto(p.Key, p.Label, p.Type)).ToArray()
            ))
            .ToArray();

        return Task.FromResult(Result<ListAgentsResponse>.Success(new ListAgentsResponse(agents)));
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/admin/bots/agents", async (
            [FromServices] ListAgentsHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ListAgentsQuery(), ct))
            .RequireAuthorization(Policies.Admin)
            .WithName("ListBotAgents")
            .WithTags("Admin", "Bots");
    }
}
