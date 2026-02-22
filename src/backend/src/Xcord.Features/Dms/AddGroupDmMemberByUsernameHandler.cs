using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Dms;

public sealed record AddGroupDmMemberByUsernameRequest(
    long DmChannelId,
    string Username
);

public sealed class AddGroupDmMemberByUsernameHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IOutboxWriter outboxWriter,
    ILogger<AddGroupDmMemberByUsernameHandler> logger)
    : IRequestHandler<AddGroupDmMemberByUsernameRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(AddGroupDmMemberByUsernameRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Error.Validation("USERNAME_REQUIRED", "Username is required");

        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var currentUserId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        // Get DM channel
        var dmChannel = await dbContext.DmChannels
            .Include(dm => dm.Members)
            .FirstOrDefaultAsync(dm => dm.Id == request.DmChannelId, cancellationToken);

        if (dmChannel == null)
            return Error.NotFound("DM_NOT_FOUND", "DM channel not found");

        if (!dmChannel.IsGroup)
            return Error.Validation("NOT_GROUP_DM", "Cannot add members to 1:1 DM channels");

        if (dmChannel.OwnerId != currentUserId)
            return Error.Forbidden("NOT_OWNER", "Only the group DM owner can add members");

        // Look up user by username
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user == null)
            return Error.NotFound("USER_NOT_FOUND", "User not found");

        // Check if already a member
        if (dmChannel.Members.Any(m => m.UserId == user.Id))
            return Error.Conflict("ALREADY_MEMBER", "User is already a member of this DM channel");

        // Check max member count
        if (dmChannel.Members.Count >= 10)
            return Error.Validation("MAX_MEMBERS", "Group DM channels cannot have more than 10 members");

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.DmChannelMembers.Add(new DmChannelMember
            {
                UserId = user.Id,
                DmChannelId = request.DmChannelId,
                JoinedAt = DateTimeOffset.UtcNow
            });

            await outboxWriter.WriteAsync(dbContext, "Dm.MemberAdded", new
            {
                DmChannelId = request.DmChannelId,
                UserId = user.Id,
                AddedBy = currentUserId
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "User {CurrentUserId} added user {UserId} (username: {Username}) to group DM {DmChannelId}",
                currentUserId, user.Id, request.Username, request.DmChannelId);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/users/@me/dms/{dmChannelId:long}/members", async (
            long dmChannelId,
            [FromBody] AddGroupDmMemberByUsernameRequest request,
            [FromServices] AddGroupDmMemberByUsernameHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(
                new AddGroupDmMemberByUsernameRequest(dmChannelId, request.Username), ct,
                onSuccess: _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("AddGroupDmMemberByUsername")
        .WithTags("DMs");
}
