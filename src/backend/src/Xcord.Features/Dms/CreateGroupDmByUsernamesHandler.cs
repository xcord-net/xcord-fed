using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Dms;

public sealed record CreateGroupDmByUsernamesRequest(
    string[] Usernames,
    string? Name
);

public sealed class CreateGroupDmByUsernamesHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    INotificationService notificationService,
    ILogger<CreateGroupDmByUsernamesHandler> logger)
    : IRequestHandler<CreateGroupDmByUsernamesRequest, Result<CreateDmResponse>>
{
    public async Task<Result<CreateDmResponse>> Handle(
        CreateGroupDmByUsernamesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Usernames == null || request.Usernames.Length < 2)
            return Error.Validation("VALIDATION_FAILED", "At least 2 usernames are required to create a group DM");

        if (request.Usernames.Length > 9)
            return Error.Validation("VALIDATION_FAILED", "Cannot add more than 9 other members to a group DM");

        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

        // Look up all recipients by username
        var distinctUsernames = request.Usernames.Distinct().ToList();
        var recipients = await dbContext.Users
            .Where(u => distinctUsernames.Contains(u.Username))
            .ToListAsync(cancellationToken);

        if (recipients.Count != distinctUsernames.Count)
        {
            var notFound = distinctUsernames.Where(u => recipients.All(r => r.Username != u)).ToArray();
            return Error.NotFound("USER_NOT_FOUND", $"Users not found: {string.Join(", ", notFound)}");
        }

        var recipientIds = recipients.Select(r => r.Id).ToArray();
        var allMemberIds = recipientIds.Append(currentUserId).Distinct().ToList();

        var now = DateTimeOffset.UtcNow;

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
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
                IsGroup = true,
                Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
                OwnerId = currentUserId
            };
            dbContext.DmChannels.Add(dmChannel);

            foreach (var memberId in allMemberIds)
            {
                dbContext.DmChannelMembers.Add(new DmChannelMember
                {
                    UserId = memberId,
                    DmChannelId = dmChannelId,
                    JoinedAt = now
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Notify each member directly after save
            var dmCreatedPayload = new
            {
                DmChannelId = dmChannelId,
                ConversationId = conversationId,
                MemberIds = allMemberIds.ToArray()
            };
            foreach (var memberId in allMemberIds)
            {
                await notificationService.NotifyUserAsync(memberId, "Notify_DmCreated", dmCreatedPayload, cancellationToken).ConfigureAwait(false);
            }

            logger.LogInformation(
                "User {UserId} created group DM {DmChannelId} with members: {MemberIds}",
                currentUserId, dmChannelId, string.Join(", ", allMemberIds));

            var allUsers = await dbContext.Users
                .Where(u => allMemberIds.Contains(u.Id))
                .ToListAsync(cancellationToken);

            var members = allUsers
                .Select(u => new DmMemberDto(u.Id, u.Username, u.DisplayName, u.AvatarUrl, now))
                .ToArray();

            return new CreateDmResponse(
                dmChannelId,
                conversationId,
                true,
                dmChannel.Name,
                currentUserId,
                members
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/dms/group", async (
            [FromBody] CreateGroupDmByUsernamesRequest request,
            [FromServices] CreateGroupDmByUsernamesHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(request, ct, success => Results.Created($"/api/v1/dms/{success.Id}", success)))
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("CreateGroupDmByUsernames")
            .WithTags("DMs");
}
