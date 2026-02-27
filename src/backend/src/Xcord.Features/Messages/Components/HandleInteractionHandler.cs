using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Messages;

public sealed record HandleInteractionCommand(long ComponentId, string? Value);
public sealed record HandleInteractionRequest(string? Value);
public sealed record InteractionResponse(long ComponentId, string Status);

public sealed class HandleInteractionHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IOutboxWriter outboxWriter)
    : IRequestHandler<HandleInteractionCommand, Result<InteractionResponse>>
{
    public async Task<Result<InteractionResponse>> Handle(HandleInteractionCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var component = await dbContext.MessageComponents.AsNoTracking()
            .Include(c => c.Message)
            .FirstOrDefaultAsync(c => c.Id == request.ComponentId, ct);
        if (component == null) return Error.NotFound("COMPONENT_NOT_FOUND", "Component not found");
        if (component.Disabled) return Error.Validation("COMPONENT_DISABLED", "This component is disabled");

        // Resolve the BotToken for the message author so the dispatcher can find the endpoint URL.
        long? botTokenId = null;
        if (component.Message.AuthorId.HasValue)
        {
            var botToken = await dbContext.BotTokens.AsNoTracking()
                .Where(bt => bt.UserId == component.Message.AuthorId.Value && !bt.IsRevoked)
                .OrderByDescending(bt => bt.CreatedAt)
                .Select(bt => new { bt.Id })
                .FirstOrDefaultAsync(ct);
            botTokenId = botToken?.Id;
        }

        // Write interaction event to outbox for delivery to the bot's endpoint.
        await outboxWriter.WriteAsync(dbContext, "Bot_Interaction", new
        {
            botTokenId,
            componentId = component.Id,
            componentType = component.ComponentType.ToString(),
            customId = component.CustomId,
            messageId = component.MessageId,
            conversationId = component.Message.ConversationId,
            userId,
            value = request.Value,
            timestamp = DateTimeOffset.UtcNow
        }, ct);

        await dbContext.SaveChangesAsync(ct);

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
