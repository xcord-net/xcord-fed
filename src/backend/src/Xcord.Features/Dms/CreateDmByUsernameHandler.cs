using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

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
    IRequestHandler<CreateDmRequest, Result<CreateDmResponse>> createDmHandler)
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

        // Delegate to the ID-based handler
        var result = await createDmHandler.Handle(new CreateDmRequest([recipient.Id]), cancellationToken);

        if (!result.IsSuccess)
            return result.Error;

        var dm = result.Value;
        var recipientMember = dm.Members.FirstOrDefault(m => m.UserId == recipient.Id);

        return new CreateDmByUsernameResponse(
            dm.Id,
            dm.ConversationId,
            recipient.Id,
            recipient.Username,
            recipientMember?.AvatarUrl ?? recipient.AvatarUrl,
            DateTimeOffset.FromUnixTimeMilliseconds(dm.Id >> 22)
        );
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
