using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Bots;

public sealed record RegisterCommandCommand(long ServerId, string Name, string? Description, string? OptionsJson);
public sealed record RegisterCommandRequest(string Name, string? Description, string? OptionsJson);
public sealed record SlashCommandResponse(long Id, long ServerId, string Name, string? Description, string OptionsJson, DateTimeOffset CreatedAt);

public sealed class RegisterCommandHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<RegisterCommandCommand, Result<SlashCommandResponse>>, IValidatable<RegisterCommandCommand>
{
    public Error? Validate(RegisterCommandCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return Error.Validation("VALIDATION_ERROR", "Name is required");
        if (r.Name.Length > 32) return Error.Validation("VALIDATION_ERROR", "Name must be 32 characters or less");
        return null;
    }

    public async Task<Result<SlashCommandResponse>> Handle(RegisterCommandCommand request, CancellationToken ct)
    {
        var botClaim = httpContextAccessor.HttpContext?.User.FindFirst("bot_token_id")?.Value;
        if (string.IsNullOrEmpty(botClaim) || !long.TryParse(botClaim, out var botTokenId))
            return Error.Forbidden("UNAUTHORIZED", "Bot token required");

        var exists = await dbContext.SlashCommands.AsNoTracking()
            .AnyAsync(c => c.ServerId == request.ServerId && c.Name == request.Name, ct);
        if (exists) return Error.Conflict("COMMAND_EXISTS", "A command with this name already exists");

        var now = DateTimeOffset.UtcNow;
        var cmd = new SlashCommand
        {
            Id = snowflakeGenerator.NextId(), BotTokenId = botTokenId, ServerId = request.ServerId,
            Name = request.Name, Description = request.Description,
            OptionsJson = request.OptionsJson ?? "[]", CreatedAt = now
        };
        dbContext.SlashCommands.Add(cmd);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return new SlashCommandResponse(cmd.Id, cmd.ServerId, cmd.Name, cmd.Description, cmd.OptionsJson, cmd.CreatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/servers/{serverId}/commands", async (
            long serverId, RegisterCommandRequest request,
            IRequestHandler<RegisterCommandCommand, Result<SlashCommandResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new RegisterCommandCommand(serverId, request.Name, request.Description, request.OptionsJson), ct))
        .RequireAuthorization(Policies.Bot)
        .WithName("RegisterCommand").WithTags("SlashCommands");
}
