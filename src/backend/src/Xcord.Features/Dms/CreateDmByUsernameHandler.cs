using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Dms;

public sealed record CreateDmByUsernameRequest(string Username);

/// <summary>
/// Response DTO shaped for the frontend DmChannel type.
/// </summary>
public sealed record CreateDmByUsernameResponse(
    long Id,
    long ConversationId,
    long RecipientId,
    string RecipientUsername,
    string? RecipientAvatarUrl,
    DateTimeOffset CreatedAt
);

public sealed class CreateDmByUsernameHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    SnowflakeIdGenerator snowflakeGenerator,
    IOutboxWriter outboxWriter)
    : IRequestHandler<CreateDmByUsernameRequest, Result<CreateDmByUsernameResponse>>
{
    public async Task<Result<CreateDmByUsernameResponse>> Handle(
        CreateDmByUsernameRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Error.Validation("USERNAME_REQUIRED", "Username is required");

        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var currentUserId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        // Look up recipient by username
        var recipient = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (recipient == null)
            return Error.NotFound("USER_NOT_FOUND", "User not found");

        if (recipient.Id == currentUserId)
            return Error.Validation("CANNOT_DM_SELF", "You cannot open a DM with yourself");

        // Check if a block exists in either direction
        var blockExists = await dbContext.UserBlocks
            .AnyAsync(b =>
                (b.BlockerId == currentUserId && b.BlockedId == recipient.Id) ||
                (b.BlockerId == recipient.Id && b.BlockedId == currentUserId),
                cancellationToken);

        if (blockExists)
            return Error.Forbidden("USER_BLOCKED", "Cannot open a DM with a blocked user");

        // Check for existing 1:1 DM between these two users
        var existingDm = await dbContext.DmChannels
            .Where(dm => !dm.IsGroup)
            .Where(dm => dm.Members.Any(m => m.UserId == currentUserId) &&
                         dm.Members.Any(m => m.UserId == recipient.Id))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingDm != null)
        {
            return new CreateDmByUsernameResponse(
                existingDm.Id,
                existingDm.ConversationId,
                recipient.Id,
                recipient.Username,
                recipient.AvatarUrl,
                DateTimeOffset.FromUnixTimeMilliseconds(existingDm.Id >> 22)
            );
        }

        // Create new 1:1 DM
        var now = DateTimeOffset.UtcNow;

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var conversationId = snowflakeGenerator.NextId();
            var conversation = new Conversation
            {
                Id = conversationId,
                Type = ConversationType.DmChannel
            };
            dbContext.Conversations.Add(conversation);

            var dmChannelId = snowflakeGenerator.NextId();
            var dmChannel = new DmChannel
            {
                Id = dmChannelId,
                ConversationId = conversationId,
                IsGroup = false,
                Name = null,
                OwnerId = null
            };
            dbContext.DmChannels.Add(dmChannel);

            dbContext.DmChannelMembers.AddRange(
                new DmChannelMember { UserId = currentUserId, DmChannelId = dmChannelId, JoinedAt = now },
                new DmChannelMember { UserId = recipient.Id, DmChannelId = dmChannelId, JoinedAt = now }
            );

            await outboxWriter.WriteAsync(dbContext, "Dm.Created", new
            {
                DmChannelId = dmChannelId,
                ConversationId = conversationId,
                MemberIds = new[] { currentUserId, recipient.Id }
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CreateDmByUsernameResponse(
                dmChannelId,
                conversationId,
                recipient.Id,
                recipient.Username,
                recipient.AvatarUrl,
                now
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/dms", async (
            [FromBody] CreateDmByUsernameRequest request,
            [FromServices] CreateDmByUsernameHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(request, ct))
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("CreateDmByUsername")
            .WithTags("DMs");
}
