using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Bots.Commands;

public sealed record DeleteCommandCommand(long ServerId, long CommandId);
public sealed record DeleteCommandResponse(bool Deleted);

public sealed class DeleteCommandHandler(
    AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<DeleteCommandCommand, Result<DeleteCommandResponse>>
{
    public async Task<Result<DeleteCommandResponse>> Handle(DeleteCommandCommand request, CancellationToken ct)
    {
        var botClaim = httpContextAccessor.HttpContext?.User.FindFirst("bot_token_id")?.Value;
        if (string.IsNullOrEmpty(botClaim) || !long.TryParse(botClaim, out var botTokenId))
            return Error.Forbidden("UNAUTHORIZED", "Bot token required");

        var cmd = await dbContext.SlashCommands.FirstOrDefaultAsync(
            c => c.Id == request.CommandId && c.ServerId == request.ServerId && c.BotTokenId == botTokenId, ct);
        if (cmd == null) return Error.NotFound("COMMAND_NOT_FOUND", "Command not found");

        cmd.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return new DeleteCommandResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/servers/{serverId}/commands/{commandId}", async (
            long serverId, long commandId,
            IRequestHandler<DeleteCommandCommand, Result<DeleteCommandResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new DeleteCommandCommand(serverId, commandId), ct))
        .RequireAuthorization(Policies.Bot)
        .WithName("DeleteCommand").WithTags("SlashCommands");
}
