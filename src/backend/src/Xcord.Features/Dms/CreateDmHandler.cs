using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Dms;

public sealed record CreateDmRequest(
    long[] RecipientIds
);

public sealed class CreateDmHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    ILogger<CreateDmHandler> logger) : IRequestHandler<CreateDmRequest, Result<CreateDmResponse>>, IValidatable<CreateDmRequest>
{
    public Error? Validate(CreateDmRequest request)
    {
        if (request.RecipientIds == null || request.RecipientIds.Length == 0)
            return Error.Validation("VALIDATION_FAILED", "RecipientIds is required");

        if (request.RecipientIds.Length < 1 || request.RecipientIds.Length > 9)
            return Error.Validation("VALIDATION_FAILED", "RecipientIds must contain 1-9 entries (max 10 members including you)");

        if (request.RecipientIds.Distinct().Count() != request.RecipientIds.Length)
            return Error.Validation("VALIDATION_FAILED", "RecipientIds must not contain duplicates");

        return null;
    }

    public async Task<Result<CreateDmResponse>> Handle(CreateDmRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

        // Verify all recipient IDs exist
        var allUserIds = request.RecipientIds.Append(currentUserId).Distinct().ToList();
        var users = await dbContext.Users
            .Where(u => allUserIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        if (users.Count != allUserIds.Count)
        {
            return Error.NotFound("USER_NOT_FOUND", "One or more recipient users not found");
        }

        var now = DateTimeOffset.UtcNow;
        var isGroup = request.RecipientIds.Length > 1;

        // For 1:1 DMs, check if a DM already exists between current user and recipient
        if (!isGroup)
        {
            var recipientId = request.RecipientIds[0];

            // Find existing 1:1 DM between these two users
            var existingDm = await dbContext.DmChannels
                .Where(dm => !dm.IsGroup)
                .Where(dm => dm.Members.Any(m => m.UserId == currentUserId) &&
                             dm.Members.Any(m => m.UserId == recipientId))
                .Include(dm => dm.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingDm != null)
            {
                // Return existing DM channel
                var existingMembers = existingDm.Members
                    .Select(m => new DmMemberDto(
                        m.UserId,
                        m.User.Username,
                        m.User.DisplayName,
                        m.User.AvatarUrl,
                        m.JoinedAt))
                    .ToArray();

                return new CreateDmResponse(
                    existingDm.Id,
                    existingDm.ConversationId,
                    existingDm.IsGroup,
                    existingDm.Name,
                    existingDm.OwnerId,
                    existingMembers
                );
            }
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Create Conversation
            var conversationId = snowflakeGenerator.NextId();
            var conversation = new Conversation
            {
                Id = conversationId,
                Type = ConversationType.DmChannel
            };

            dbContext.Conversations.Add(conversation);

            // Create DmChannel
            var dmChannelId = snowflakeGenerator.NextId();
            var dmChannel = new DmChannel
            {
                Id = dmChannelId,
                ConversationId = conversationId,
                IsGroup = isGroup,
                Name = null, // Group name can be set later via update
                OwnerId = isGroup ? currentUserId : null
            };

            dbContext.DmChannels.Add(dmChannel);

            // Add all members (current user + recipients)
            var memberIds = new List<long> { currentUserId };
            memberIds.AddRange(request.RecipientIds);

            foreach (var memberId in memberIds.Distinct())
            {
                var member = new DmChannelMember
                {
                    UserId = memberId,
                    DmChannelId = dmChannelId,
                    JoinedAt = now
                };

                dbContext.DmChannelMembers.Add(member);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Notify each member directly after save
            var dmCreatedPayload = new
            {
                DmChannelId = dmChannelId,
                ConversationId = conversationId,
                MemberIds = memberIds.ToArray()
            };
            foreach (var memberId in memberIds.Distinct())
            {
                await notificationService.NotifyUserAsync(memberId, "Notify_DmCreated", dmCreatedPayload);
            }

            logger.LogInformation(
                "User {UserId} created DM channel {DmChannelId} (IsGroup: {IsGroup}) with members: {MemberIds}",
                currentUserId, dmChannelId, isGroup, string.Join(", ", memberIds));

            // Build response with member details
            var members = users
                .Where(u => memberIds.Contains(u.Id))
                .Select(u => new DmMemberDto(
                    u.Id,
                    u.Username,
                    u.DisplayName,
                    u.AvatarUrl,
                    now))
                .ToArray();

            return new CreateDmResponse(
                dmChannelId,
                conversationId,
                isGroup,
                null,
                isGroup ? currentUserId : null,
                members
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/users/@me/dms", async (
            [FromBody] CreateDmRequest request,
            [FromServices] CreateDmHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(request, ct))
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("CreateDm")
            .WithTags("DMs");
}
