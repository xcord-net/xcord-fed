using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Notes;

public sealed record DeleteUserNoteRequest(long TargetUserId);

public sealed record DeleteUserNoteResponse(bool Deleted);

public sealed class DeleteUserNoteHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DeleteUserNoteHandler> logger)
    : IRequestHandler<DeleteUserNoteRequest, Result<DeleteUserNoteResponse>>
{
    public async Task<Result<DeleteUserNoteResponse>> Handle(DeleteUserNoteRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        var note = await dbContext.UserNotes
            .FirstOrDefaultAsync(n => n.AuthorId == userId && n.TargetUserId == request.TargetUserId, cancellationToken);

        if (note == null)
        {
            return Error.NotFound("NOTE_NOT_FOUND", "No note found for this user");
        }

        note.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} deleted note about user {TargetUserId}", userId, request.TargetUserId);

        return new DeleteUserNoteResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/users/{targetUserId:long}/notes", async (
            long targetUserId,
            [FromServices] DeleteUserNoteHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new DeleteUserNoteRequest(targetUserId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteUserNote")
        .WithTags("Notes");
}
