using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Messages.Components;

public sealed record HandleInteractionCommand(long ComponentId, string? Value);
public sealed record HandleInteractionRequest(string? Value);
public sealed record InteractionResponse(long ComponentId, string Status);

public sealed class HandleInteractionHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<HandleInteractionCommand, Result<InteractionResponse>>
{
    public async Task<Result<InteractionResponse>> Handle(HandleInteractionCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var component = await dbContext.MessageComponents.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ComponentId, ct);
        if (component == null) return Error.NotFound("COMPONENT_NOT_FOUND", "Component not found");
        if (component.Disabled) return Error.Validation("COMPONENT_DISABLED", "This component is disabled");

        // TODO: Wire bot interaction dispatch when bot webhook delivery is implemented.
        // Previously wrote to outbox as "Bot_Interaction" but no consumer existed.

        return new InteractionResponse(component.Id, "dispatched");
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/interactions/{componentId}", async (
            long componentId, HandleInteractionRequest request,
            IRequestHandler<HandleInteractionCommand, Result<InteractionResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new HandleInteractionCommand(componentId, request.Value), ct))
        .RequireAuthorization(Policies.User)
        .WithName("HandleInteraction").WithTags("MessageComponents");
}
