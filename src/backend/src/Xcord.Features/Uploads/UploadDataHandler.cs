using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Uploads;

/// <summary>
/// Proxy endpoint: receives file data from the browser and uploads to S3.
/// This avoids requiring the browser to have direct access to the S3/MinIO endpoint.
/// </summary>
public sealed class UploadDataHandler : IEndpoint
{
    private const long MaxFileSize = 25 * 1024 * 1024; // 25MB

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPut("/api/v1/uploads/{attachmentId}/data", async (
            long attachmentId,
            AppDbContext dbContext,
            IStorageService storageService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out _))
            {
                return Results.Json(new { error = "UNAUTHORIZED", message = "User is not authenticated" }, statusCode: 401);
            }

            var attachment = await dbContext.Attachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

            if (attachment == null)
            {
                return Results.Json(new { error = "ATTACHMENT_NOT_FOUND", message = "Attachment not found" }, statusCode: 404);
            }

            if (attachment.IsConfirmed)
            {
                return Results.Json(new { error = "ALREADY_UPLOADED", message = "File has already been uploaded" }, statusCode: 409);
            }

            if (httpContext.Request.ContentLength > MaxFileSize)
            {
                return Results.Json(new { error = "FILE_TOO_LARGE", message = "File exceeds maximum size" }, statusCode: 413);
            }

            using var ms = new MemoryStream();
            await httpContext.Request.Body.CopyToAsync(ms, ct);
            var data = ms.ToArray();

            if (data.Length == 0)
            {
                return Results.Json(new { error = "EMPTY_BODY", message = "Request body is empty" }, statusCode: 400);
            }

            if (data.Length > MaxFileSize)
            {
                return Results.Json(new { error = "FILE_TOO_LARGE", message = "File exceeds maximum size" }, statusCode: 413);
            }

            await storageService.UploadAsync(attachment.S3Key, data, attachment.ContentType);

            return Results.Ok();
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UploadData")
        .WithTags("Uploads")
        .DisableAntiforgery();
    }
}
