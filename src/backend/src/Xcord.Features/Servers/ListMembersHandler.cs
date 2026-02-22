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

public sealed record RoleDto(
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
    List<RoleDto> Roles,
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
        var isMember = await dbContext.ServerMembers
            .AsNoTracking()
            .AnyAsync(sm => sm.UserId == userId && sm.ServerId == request.ServerId, cancellationToken);

        if (!isMember)
        {
            return Error.Forbidden("NOT_A_MEMBER", "You must be a member of this server to view its members");
        }

        // Query members with user and role data
        var members = await dbContext.ServerMembers
            .AsNoTracking()
            .Where(sm => sm.ServerId == request.ServerId)
            .Include(sm => sm.User)
            .Include(sm => sm.MemberRoles)
                .ThenInclude(mr => mr.Role)
            .Select(sm => new MemberDto(
                sm.UserId,
                sm.User.Username,
                sm.User.DisplayName,
                sm.User.AvatarUrl,
                sm.Nickname,
                sm.MemberRoles
                    .Select(mr => new RoleDto(
                        mr.Role.Id,
                        mr.Role.Name,
                        mr.Role.Color,
                        mr.Role.Position
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
            return await handler.ExecuteAsync(new ListMembersRequest(serverId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ListMembers")
        .WithTags("Members");
}
