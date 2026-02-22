using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Messages.Components;

public sealed record ListComponentsQuery(long ConversationId, long MessageId);

public sealed class ListComponentsHandler(AppDbContext dbContext)
    : IRequestHandler<ListComponentsQuery, Result<List<ComponentResponse>>>
{
    public async Task<Result<List<ComponentResponse>>> Handle(ListComponentsQuery request, CancellationToken ct)
    {
        var components = await dbContext.MessageComponents.AsNoTracking()
            .Where(c => c.MessageId == request.MessageId)
            .OrderBy(c => c.Row).ThenBy(c => c.Position)
            .Select(c => new ComponentResponse(c.Id, c.MessageId, c.ComponentType.ToString(),
                c.CustomId, c.Label, c.Style, c.OptionsJson, c.Disabled, c.Row, c.Position))
            .ToListAsync(ct);
        return components;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/conversations/{conversationId}/messages/{messageId}/components", async (
            long conversationId, long messageId,
            IRequestHandler<ListComponentsQuery, Result<List<ComponentResponse>>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ListComponentsQuery(conversationId, messageId), ct))
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListComponents").WithTags("MessageComponents");
}
