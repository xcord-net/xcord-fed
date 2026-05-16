using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Notes;

public sealed record DeleteUserNoteRequest(long TargetUserId);

public sealed record DeleteUserNoteResponse(bool Deleted);

public sealed class DeleteUserNoteHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<DeleteUserNoteHandler> logger)
    : IRequestHandler<DeleteUserNoteRequest, Result<DeleteUserNoteResponse>>
{
    public async Task<Result<DeleteUserNoteResponse>> Handle(DeleteUserNoteRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var note = await dbContext.UserNotes
            .FirstOrDefaultAsync(n => n.AuthorId == userId && n.TargetUserId == request.TargetUserId, cancellationToken);

        if (note == null)
        {
            return Error.NotFound("NOTE_NOT_FOUND", "No note found for this user");
        }

        note.SoftDelete();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User {UserId} deleted note about user {TargetUserId}", userId, request.TargetUserId);

        return new DeleteUserNoteResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/users/{targetUserId:long}/notes", async (
            long targetUserId,
            [FromServices] DeleteUserNoteHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new DeleteUserNoteRequest(targetUserId), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteUserNote")
        .WithTags("Notes");
}
