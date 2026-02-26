using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record LeaveServerCommand(long ServerId);

public sealed class LeaveServerHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IOutboxWriter outboxWriter,
    ICurrentUserService currentUserService,
    ILogger<LeaveServerHandler> logger)
    : IRequestHandler<LeaveServerCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(LeaveServerCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check if server exists
        var server = await dbContext.Servers
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server == null)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Cannot leave if you are the owner
        if (server.OwnerId == userId)
        {
            return Error.Validation("OWNER_CANNOT_LEAVE", "Server owner cannot leave the server. Transfer ownership or delete the server instead.");
        }

        // Find server member
        var serverMember = await dbContext.ServerMembers
            .FirstOrDefaultAsync(sm => sm.UserId == userId && sm.ServerId == request.ServerId, cancellationToken);

        if (serverMember == null)
        {
            return Error.NotFound("NOT_A_MEMBER", "You are not a member of this server");
        }

        // Remove server member
        dbContext.ServerMembers.Remove(serverMember);

        // Decrement server member count atomically
        server.MemberCount--;

        // Create system message (MemberLeave) in the server's system channel if configured
        if (server.SystemChannelId.HasValue)
        {
            var systemChannel = await dbContext.Channels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == server.SystemChannelId.Value, cancellationToken);

            if (systemChannel != null)
            {
                var user = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

                var messageId = snowflakeGenerator.NextId();
                var systemMessage = new Message
                {
                    Id = messageId,
                    ConversationId = systemChannel.ConversationId,
                    AuthorId = null,
                    Type = MessageType.MemberLeave,
                    Content = string.Empty,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        UserId = userId.ToString(),
                        Username = user?.Username ?? string.Empty,
                        DisplayName = user?.DisplayName ?? string.Empty
                    }),
                    CreatedAt = DateTimeOffset.UtcNow
                };

                dbContext.Messages.Add(systemMessage);

                await outboxWriter.WriteAsync(dbContext, "Message.Created", new
                {
                    MessageId = systemMessage.Id,
                    ConversationId = systemMessage.ConversationId,
                    AuthorId = (long?)null
                }, cancellationToken);
            }
        }

        // Write Member.Left outbox event (used by outgoing webhooks)
        await outboxWriter.WriteAsync(dbContext, "Member.Left", new
        {
            ServerId = request.ServerId,
            UserId = userId
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} left server {ServerId}",
            userId, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{id:long}/members/@me", async (
            long id,
            IRequestHandler<LeaveServerCommand, Result<bool>> handler,
            CancellationToken ct) =>
        {
            var command = new LeaveServerCommand(id);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("LeaveServer")
        .WithTags("Servers", "Members");
    }
}
