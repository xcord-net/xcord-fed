using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Messages;

public sealed record AddComponentCommand(long ConversationId, long MessageId, string ComponentType, string? CustomId, string? Label, int? Style, string? OptionsJson, bool Disabled, int Row, int Position);
public sealed record AddComponentRequest(string ComponentType, string? CustomId, string? Label, int? Style, string? OptionsJson, bool Disabled, int Row, int Position);
public sealed record ComponentResponse(long Id, long MessageId, string ComponentType, string? CustomId, string? Label, int? Style, string? OptionsJson, bool Disabled, int Row, int Position);

public sealed class AddComponentHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<AddComponentCommand, Result<ComponentResponse>>, IValidatable<AddComponentCommand>
{
    public Error? Validate(AddComponentCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.ComponentType)) return Error.Validation("VALIDATION_ERROR", "ComponentType is required");
        if (r.Row < 0 || r.Row > 4) return Error.Validation("VALIDATION_ERROR", "Row must be between 0 and 4");
        return null;
    }

    public async Task<Result<ComponentResponse>> Handle(AddComponentCommand request, CancellationToken ct)
    {
        var botClaim = httpContextAccessor.HttpContext?.User.FindFirst("bot_token_id")?.Value;
        if (string.IsNullOrEmpty(botClaim)) return Error.Forbidden("UNAUTHORIZED", "Bot token required");

        var message = await dbContext.Messages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.ConversationId == request.ConversationId, ct);
        if (message == null) return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found");

        if (!Enum.TryParse<ComponentType>(request.ComponentType, true, out var componentType))
            return Error.Validation("INVALID_TYPE", "Invalid component type");

        var now = DateTimeOffset.UtcNow;
        var component = new MessageComponent
        {
            Id = snowflakeGenerator.NextId(), MessageId = request.MessageId,
            ComponentType = componentType, CustomId = request.CustomId, Label = request.Label,
            Style = request.Style, OptionsJson = request.OptionsJson, Disabled = request.Disabled,
            Row = request.Row, Position = request.Position, CreatedAt = now
        };
        dbContext.MessageComponents.Add(component);
        await dbContext.SaveChangesAsync(ct);

        return new ComponentResponse(component.Id, component.MessageId, component.ComponentType.ToString(),
            component.CustomId, component.Label, component.Style, component.OptionsJson,
            component.Disabled, component.Row, component.Position);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/conversations/{conversationId}/messages/{messageId}/components", async (
            long conversationId, long messageId, AddComponentRequest request,
            IRequestHandler<AddComponentCommand, Result<ComponentResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new AddComponentCommand(conversationId, messageId, request.ComponentType,
                request.CustomId, request.Label, request.Style, request.OptionsJson,
                request.Disabled, request.Row, request.Position), ct))
        .RequireAuthorization(Policies.Bot)
        .WithName("AddComponent").WithTags("MessageComponents");
}
