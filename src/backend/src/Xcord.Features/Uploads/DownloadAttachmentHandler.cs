using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Uploads;

internal static class AttachmentResponseHeaders
{
    // Content types we'll allow to render inline. Everything else forces a download.
    // Aligned with the upload allowlist in RequestUploadHandler (image/jpeg, png, gif, webp).
    public static readonly HashSet<string> InlineSafeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
    };

    public static void ApplyDownloadHeaders(HttpResponse response, string contentType, string fileName)
    {
        // Block MIME sniffing so a misdeclared file (e.g. HTML stored as image/png) cannot be rendered as HTML.
        response.Headers["X-Content-Type-Options"] = "nosniff";

        var disposition = InlineSafeTypes.Contains(contentType) ? "inline" : "attachment";
        // RFC 5987 encoding of the filename to handle non-ASCII safely.
        var encoded = Uri.EscapeDataString(fileName);
        response.Headers["Content-Disposition"] = $"{disposition}; filename*=UTF-8''{encoded}";
    }
}

/// <summary>
/// Proxy endpoint: streams a confirmed attachment from S3 to the browser.
/// This avoids requiring the browser to have direct access to the S3/MinIO endpoint
/// and provides stable (non-expiring) URLs for emojis and other server assets.
/// </summary>
public sealed class DownloadAttachmentHandler : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/attachments/{attachmentId}/download", async (
            long attachmentId,
            HttpContext httpContext,
            AppDbContext dbContext,
            IStorageService storageService,
            ICurrentUserService currentUserService,
            IConversationResolver conversationResolver,
            CancellationToken ct) =>
        {
            if (attachmentId <= 0)
                return Results.Json(new { error = "VALIDATION_ERROR", message = "Invalid attachment id" }, statusCode: 400);
            var userIdResult = currentUserService.GetCurrentUserId();
            if (userIdResult.IsFailure)
                return Results.Json(new { error = "UNAUTHORIZED", message = "User is not authenticated" }, statusCode: 401);

            var userId = userIdResult.Value;

            var attachment = await dbContext.Attachments
                .AsNoTracking()
                .Include(a => a.Message)
                .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

            if (attachment == null)
            {
                return Results.Json(new { error = "ATTACHMENT_NOT_FOUND", message = "Attachment not found" }, statusCode: 404);
            }

            if (!attachment.IsConfirmed)
            {
                return Results.Json(new { error = "ATTACHMENT_NOT_CONFIRMED", message = "Attachment has not been uploaded" }, statusCode: 404);
            }

            // Verify the user has access to the conversation containing this attachment
            if (attachment.Message != null)
            {
                var accessResult = await conversationResolver.ResolveAsync(
                    attachment.Message.ConversationId, userId, Role.ReadMessageHistory, ct);
                if (accessResult.IsFailure)
                    return Results.Json(new { error = "FORBIDDEN", message = "You do not have access to this attachment" }, statusCode: 403);
            }

            // Stream the file bytes directly from S3 to avoid presigned URL TTL issues.
            var data = await storageService.DownloadAsync(attachment.S3Key);

            AttachmentResponseHeaders.ApplyDownloadHeaders(httpContext.Response, attachment.ContentType, attachment.FileName);
            return Results.File(data, attachment.ContentType);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DownloadAttachment")
        .WithTags("Uploads");
    }
}
