using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Servers;

public sealed record ListMembersRequest(long ServerId);

public sealed record MemberGroupDto(
    long Id,
    string Name,
    string? Color,
    int Position
);

public sealed record MemberDto(
    long UserId,
    string Username,
    string? DisplayName,
    string? AvatarUrl,
    string? Nickname,
    List<MemberGroupDto> Groups,
    DateTimeOffset JoinedAt
);

public sealed class ListMembersHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<ListMembersRequest, Result<List<MemberDto>>>
{
    public async Task<Result<List<MemberDto>>> Handle(ListMembersRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Verify requesting user is a member of the server
        var memberCheck = await dbContext.EnsureMembership(request.ServerId, userId, cancellationToken).ConfigureAwait(false);
        if (memberCheck.IsFailure) return memberCheck.Error;

        // Query members with user and group data
        var members = await dbContext.ServerMembers
            .AsNoTracking()
            .Where(sm => sm.ServerId == request.ServerId)
            .Include(sm => sm.User)
            .Include(sm => sm.MemberGroups)
                .ThenInclude(mg => mg.Group)
            .Select(sm => new MemberDto(
                sm.UserId,
                sm.User.Username,
                sm.User.DisplayName,
                sm.User.AvatarUrl,
                sm.Nickname,
                sm.MemberGroups
                    .Where(mg => !mg.Group.IsEveryone)
                    .OrderByDescending(mg => mg.Group.Position)
                    .Select(mg => new MemberGroupDto(
                        mg.Group.Id,
                        mg.Group.Name,
                        mg.Group.Color,
                        mg.Group.Position
                    ))
                    .ToList(),
                sm.JoinedAt
            ))
            .ToListAsync(cancellationToken);

        return members;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/servers/{serverId:long}/members", async (
            [FromRoute] long serverId,
            [FromServices] ListMembersHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new ListMembersRequest(serverId), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListMembers")
        .WithTags("Members");
}
