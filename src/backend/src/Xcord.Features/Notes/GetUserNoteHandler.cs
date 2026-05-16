using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Notes;

public sealed record GetUserNoteRequest(long TargetUserId);

public sealed class GetUserNoteHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetUserNoteRequest, Result<UserNoteDto>>
{
    public async Task<Result<UserNoteDto>> Handle(GetUserNoteRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var note = await dbContext.UserNotes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.AuthorId == userId && n.TargetUserId == request.TargetUserId, cancellationToken);

        if (note == null)
        {
            return Error.NotFound("NOTE_NOT_FOUND", "No note found for this user");
        }

        return new UserNoteDto(
            note.Id,
            note.TargetUserId,
            note.Content,
            note.CreatedAt,
            note.UpdatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/{targetUserId:long}/notes", async (
            long targetUserId,
            [FromServices] GetUserNoteHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(new GetUserNoteRequest(targetUserId), ct).ConfigureAwait(false);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetUserNote")
        .WithTags("Notes");
}
