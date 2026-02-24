using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers.VanityUrl;

public sealed record JoinByVanityCommand(string Slug);
public sealed record JoinByVanityResponse(long ServerId, string ServerName);

public sealed class JoinByVanityHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<JoinByVanityCommand, Result<JoinByVanityResponse>>
{
    public async Task<Result<JoinByVanityResponse>> Handle(JoinByVanityCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var server = await dbContext.Servers.FirstOrDefaultAsync(s => s.VanitySlug == request.Slug.ToLowerInvariant(), ct);
        if (server == null) return Error.NotFound("SERVER_NOT_FOUND", "No server with this vanity URL");

        var isBanned = await dbContext.Bans.AsNoTracking().AnyAsync(b => b.ServerId == server.Id && b.UserId == userId, ct);
        if (isBanned) return Error.Forbidden("BANNED", "You are banned from this server");

        var alreadyMember = await dbContext.ServerMembers.AsNoTracking()
            .AnyAsync(m => m.ServerId == server.Id && m.UserId == userId, ct);

        if (!alreadyMember)
        {
            var member = new ServerMember
            {
                ServerId = server.Id,
                UserId = userId, JoinedAt = DateTimeOffset.UtcNow
            };
            dbContext.ServerMembers.Add(member);
            server.MemberCount++;

            // Create ReadState rows for all text/forum channels so the unread
            // notification system can track messages the new member hasn't seen.
            // Skip any that already exist (e.g. user rejoining after a ban).
            var channelConversationIds = await dbContext.Channels
                .AsNoTracking()
                .Where(c => c.ServerId == server.Id && c.Type != ChannelType.Voice)
                .Select(c => c.ConversationId)
                .ToListAsync(ct);

            var existingReadStateConversationIds = await dbContext.ReadStates
                .AsNoTracking()
                .Where(rs => rs.UserId == userId && channelConversationIds.Contains(rs.ConversationId))
                .Select(rs => rs.ConversationId)
                .ToHashSetAsync(ct);

            foreach (var conversationId in channelConversationIds)
            {
                if (existingReadStateConversationIds.Contains(conversationId))
                    continue;

                dbContext.ReadStates.Add(new Xcord.Entities.ReadState
                {
                    UserId = userId,
                    ConversationId = conversationId,
                    UnreadCount = 0,
                    MentionCount = 0
                });
            }

            await dbContext.SaveChangesAsync(ct);
        }

        return new JoinByVanityResponse(server.Id, server.Name);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/invite/{slug}/join", async (
            string slug,
            IRequestHandler<JoinByVanityCommand, Result<JoinByVanityResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new JoinByVanityCommand(slug), ct))
        .RequireAuthorization(Policies.User)
        .WithName("JoinByVanity").WithTags("VanityUrl");
}
