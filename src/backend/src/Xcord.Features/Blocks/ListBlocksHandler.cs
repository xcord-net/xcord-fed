using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Blocks;

public sealed record ListBlocksRequest;

public sealed class ListBlocksHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor) : IRequestHandler<ListBlocksRequest, Result<List<UserBlockDto>>>
{
    public async Task<Result<List<UserBlockDto>>> Handle(ListBlocksRequest request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Get all blocks
        var blocks = await dbContext.UserBlocks
            .Include(ub => ub.Blocked)
            .Where(ub => ub.BlockerId == userId)
            .OrderByDescending(ub => ub.CreatedAt)
            .ToListAsync(cancellationToken);

        // Map to DTOs
        var dtos = blocks.Select(ub => new UserBlockDto(
            ub.BlockerId,
            ub.BlockedId,
            ub.Blocked.Username,
            ub.Blocked.DisplayName,
            ub.Blocked.AvatarUrl,
            ub.CreatedAt
        )).ToList();

        return dtos;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/@me/blocks", async (
            [FromServices] ListBlocksHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new ListBlocksRequest(), ct))
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("ListBlocks")
            .WithTags("Blocks");
}
