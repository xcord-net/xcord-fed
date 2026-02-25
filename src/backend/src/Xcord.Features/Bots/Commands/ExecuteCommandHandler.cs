using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Bots.Commands;

public sealed record ExecuteCommandCommand(long ServerId, long CommandId, string? ArgsJson);
public sealed record ExecuteCommandRequest(string? ArgsJson);
public sealed record ExecuteCommandResponse(long CommandId, string Status);

public sealed class ExecuteCommandHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IOutboxWriter outboxWriter)
    : IRequestHandler<ExecuteCommandCommand, Result<ExecuteCommandResponse>>
{
    public async Task<Result<ExecuteCommandResponse>> Handle(ExecuteCommandCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var cmd = await dbContext.SlashCommands.AsNoTracking()
            .Include(c => c.BotToken)
            .FirstOrDefaultAsync(c => c.Id == request.CommandId && c.ServerId == request.ServerId, ct);
        if (cmd == null) return Error.NotFound("COMMAND_NOT_FOUND", "Command not found");

        // Write interaction event to outbox for delivery to the bot's endpoint.
        await outboxWriter.WriteAsync(dbContext, "Bot_CommandExecuted", new
        {
            botTokenId = cmd.BotTokenId,
            commandId = cmd.Id,
            commandName = cmd.Name,
            serverId = cmd.ServerId,
            userId,
            argsJson = request.ArgsJson,
            timestamp = DateTimeOffset.UtcNow
        }, ct);

        await dbContext.SaveChangesAsync(ct);

        return new ExecuteCommandResponse(cmd.Id, "dispatched");
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/servers/{serverId}/commands/{commandId}/execute", async (
            long serverId, long commandId, ExecuteCommandRequest request,
            IRequestHandler<ExecuteCommandCommand, Result<ExecuteCommandResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ExecuteCommandCommand(serverId, commandId, request.ArgsJson), ct))
        .RequireAuthorization(Policies.User)
        .WithName("ExecuteCommand").WithTags("SlashCommands");
}
