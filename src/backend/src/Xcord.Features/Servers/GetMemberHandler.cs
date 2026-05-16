using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record GetMemberQuery(long ServerId, long UserId);

public sealed record GetMemberResponse(
    long UserId,
    string Username,
    string? DisplayName,
    string? AvatarUrl,
    string? Nickname,
    long[] GroupIds,
    DateTimeOffset JoinedAt
);

public sealed class GetMemberHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetMemberQuery, Result<GetMemberResponse>>
{
    public async Task<Result<GetMemberResponse>> Handle(GetMemberQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var currentUserId = userIdResult.Value;

        // Verify requesting user is a member of the server
        var memberCheck = await dbContext.EnsureMembership(request.ServerId, currentUserId, cancellationToken).ConfigureAwait(false);
        if (memberCheck.IsFailure) return memberCheck.Error;

        // Query the target member
        var member = await dbContext.ServerMembers
            .AsNoTracking()
            .Where(sm => sm.ServerId == request.ServerId && sm.UserId == request.UserId)
            .Include(sm => sm.User)
            .FirstOrDefaultAsync(cancellationToken);

        if (member == null)
            return Error.NotFound("MEMBER_NOT_FOUND", "Member not found in this server");

        // Get group IDs
        var groupIds = await dbContext.MemberGroups
            .AsNoTracking()
            .Where(mg => mg.UserId == request.UserId && mg.ServerId == request.ServerId)
            .Select(mg => mg.GroupId)
            .ToArrayAsync(cancellationToken);

        return new GetMemberResponse(
            UserId: member.UserId,
            Username: member.User.Username,
            DisplayName: member.User.DisplayName,
            AvatarUrl: member.User.AvatarUrl,
            Nickname: member.Nickname,
            GroupIds: groupIds,
            JoinedAt: member.JoinedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId:long}/members/{userId:long}", async (
            [FromRoute] long serverId,
            [FromRoute] long userId,
            [FromServices] GetMemberHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetMemberQuery(serverId, userId), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetMember")
        .WithTags("Members");
}
