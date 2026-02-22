using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Uploads;

public sealed record ConfirmUploadCommand(
    long AttachmentId
);

public sealed record ConfirmUploadResponse(
    bool Success
);

public sealed class ConfirmUploadHandler(
    AppDbContext dbContext,
    IStorageService storageService,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<ConfirmUploadCommand, Result<ConfirmUploadResponse>>
{
    public async Task<Result<ConfirmUploadResponse>> Handle(ConfirmUploadCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Get attachment
        var attachment = await dbContext.Attachments
            .FirstOrDefaultAsync(a => a.Id == request.AttachmentId, cancellationToken);

        if (attachment == null)
        {
            return Error.NotFound("ATTACHMENT_NOT_FOUND", "Attachment not found");
        }

        // Verify the file exists in S3
        var exists = await storageService.ExistsAsync(attachment.S3Key);
        if (!exists)
        {
            return Error.Validation("FILE_NOT_UPLOADED", "File has not been uploaded to storage");
        }

        // Mark as confirmed
        attachment.IsConfirmed = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ConfirmUploadResponse(Success: true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/attachments/{attachmentId}/confirm", async (
            long attachmentId,
            [FromServices] ConfirmUploadHandler handler,
            CancellationToken ct) =>
        {
            var command = new ConfirmUploadCommand(attachmentId);
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ConfirmUpload")
        .WithTags("Uploads");
    }
}
