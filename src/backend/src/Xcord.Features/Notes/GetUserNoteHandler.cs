using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Notes;

public sealed record GetUserNoteRequest(long TargetUserId);

public sealed class GetUserNoteHandler(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<GetUserNoteRequest, Result<UserNoteDto>>
{
    public async Task<Result<UserNoteDto>> Handle(GetUserNoteRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

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
            return await handler.ExecuteAsync(new GetUserNoteRequest(targetUserId), ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetUserNote")
        .WithTags("Notes");
}
