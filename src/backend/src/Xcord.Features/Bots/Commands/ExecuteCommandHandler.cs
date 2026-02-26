using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Bots.Commands;

public sealed record ExecuteCommandCommand(long ServerId, long CommandId, string? ArgsJson);
public sealed record ExecuteCommandRequest(string? ArgsJson);
public sealed record ExecuteCommandResponse(long CommandId, string Status);

public sealed class ExecuteCommandHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IOutboxWriter outboxWriter)
    : IRequestHandler<ExecuteCommandCommand, Result<ExecuteCommandResponse>>
{
    public async Task<Result<ExecuteCommandResponse>> Handle(ExecuteCommandCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

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
