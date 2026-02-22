using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Notes;

public sealed record UpsertUserNoteRequest(
    long TargetUserId,
    string Content
);

public sealed class UpsertUserNoteHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor,
    ILogger<UpsertUserNoteHandler> logger)
    : IRequestHandler<UpsertUserNoteRequest, Result<UserNoteDto>>, IValidatable<UpsertUserNoteRequest>
{
    public Error? Validate(UpsertUserNoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return Error.Validation("VALIDATION_FAILED", "Content is required");

        if (request.Content.Length > 2000)
            return Error.Validation("VALIDATION_FAILED", "Content cannot exceed 2000 characters");

        return null;
    }

    public async Task<Result<UserNoteDto>> Handle(UpsertUserNoteRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        if (request.TargetUserId == userId)
        {
            return Error.Validation("CANNOT_NOTE_SELF", "You cannot create a note about yourself");
        }

        // Verify target user exists
        var targetExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (!targetExists)
        {
            return Error.NotFound("USER_NOT_FOUND", "Target user not found");
        }

        var now = DateTimeOffset.UtcNow;

        // Check for existing note
        var existing = await dbContext.UserNotes
            .FirstOrDefaultAsync(n => n.AuthorId == userId && n.TargetUserId == request.TargetUserId, cancellationToken);

        if (existing != null)
        {
            existing.Content = request.Content;
            existing.UpdatedAt = now;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("User {UserId} updated note about user {TargetUserId}", userId, request.TargetUserId);

            return new UserNoteDto(
                existing.Id,
                existing.TargetUserId,
                existing.Content,
                existing.CreatedAt,
                existing.UpdatedAt);
        }

        // Create new note
        var note = new UserNote
        {
            Id = snowflakeGenerator.NextId(),
            AuthorId = userId,
            TargetUserId = request.TargetUserId,
            Content = request.Content,
            CreatedAt = now
        };

        dbContext.UserNotes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} created note about user {TargetUserId}", userId, request.TargetUserId);

        return new UserNoteDto(
            note.Id,
            note.TargetUserId,
            note.Content,
            note.CreatedAt,
            note.UpdatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/users/{targetUserId:long}/notes", async (
            long targetUserId,
            [FromBody] UpsertUserNoteBodyRequest body,
            [FromServices] UpsertUserNoteHandler handler,
            CancellationToken ct) =>
        {
            var request = new UpsertUserNoteRequest(targetUserId, body.Content);
            return await handler.ExecuteAsync(request, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpsertUserNote")
        .WithTags("Notes");
}

public sealed record UpsertUserNoteBodyRequest(string Content);
