using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Uploads;

public sealed record RequestUploadCommand(
    string FileName,
    string ContentType,
    long FileSize,
    long? MessageId
);

public sealed record RequestUploadResponse(
    long AttachmentId,
    string UploadUrl,
    string DownloadUrl
);

public sealed class RequestUploadHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeIdGenerator,
    ICurrentUserService currentUserService,
    IOptions<TierOptions> tierOptions)
    : IRequestHandler<RequestUploadCommand, Result<RequestUploadResponse>>, IValidatable<RequestUploadCommand>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        // Video
        "video/mp4",
        "video/webm",
        // Audio
        "audio/mpeg",
        "audio/ogg",
        "audio/wav",
        // Documents
        "application/pdf",
        "text/plain"
    };

    private const long MaxFileSize = 25 * 1024 * 1024; // 25MB

    public Error? Validate(RequestUploadCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return Error.Validation("VALIDATION_ERROR", "File name is required");
        }

        if (request.FileName.Length < 1 || request.FileName.Length > 256)
        {
            return Error.Validation("VALIDATION_ERROR", "File name must be between 1 and 256 characters");
        }

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            return Error.Validation("VALIDATION_ERROR", "Content type is required");
        }

        if (request.FileSize <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "File size must be greater than 0");
        }

        if (request.FileSize > MaxFileSize)
        {
            return Error.Validation("VALIDATION_ERROR", $"File size must not exceed {MaxFileSize / (1024 * 1024)}MB");
        }

        return null;
    }

    public async Task<Result<RequestUploadResponse>> Handle(RequestUploadCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Validate content type against allowlist
        if (!AllowedContentTypes.Contains(request.ContentType))
        {
            return Error.Validation("INVALID_CONTENT_TYPE", "Content type is not allowed");
        }

        // Validate file size
        if (request.FileSize > MaxFileSize)
        {
            return Error.Validation("FILE_TOO_LARGE", $"File size must not exceed {MaxFileSize / (1024 * 1024)}MB");
        }

        // If MessageId is provided, verify the message exists and user has permission
        if (request.MessageId.HasValue)
        {
            var message = await dbContext.Messages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == request.MessageId.Value, cancellationToken);

            if (message == null)
            {
                return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found");
            }

            // Verify the user is the author of the message
            if (message.AuthorId != userId)
            {
                return Error.Forbidden("NOT_MESSAGE_AUTHOR", "Only the message author can attach files");
            }
        }

        // Storage quota check: reject if adding this file would exceed the instance limit
        var maxStorageMb = tierOptions.Value.MaxStorageMb;
        if (maxStorageMb > 0)
        {
            var currentUsageBytes = await dbContext.Attachments
                .Where(a => a.IsConfirmed)
                .SumAsync(a => a.FileSize, cancellationToken);

            var maxStorageBytes = (long)maxStorageMb * 1024 * 1024;
            if (currentUsageBytes + request.FileSize > maxStorageBytes)
            {
                var usedMb = currentUsageBytes / (1024 * 1024);
                return Error.Validation("STORAGE_QUOTA_EXCEEDED",
                    $"Storage quota exceeded. {usedMb}/{maxStorageMb} MB used.");
            }
        }

        // Generate attachment ID
        var attachmentId = snowflakeIdGenerator.NextId();

        // Sanitize filename
        var sanitizedFileName = SanitizeFileName(request.FileName);

        // Generate S3 key: attachments/{yyyy}/{MM}/{snowflakeId}/{fileName}
        var now = DateTimeOffset.UtcNow;
        var s3Key = $"attachments/{now:yyyy}/{now:MM}/{attachmentId}/{sanitizedFileName}";

        // Always create attachment entity (MessageId is nullable for pre-message uploads)
        var attachment = new Attachment
        {
            Id = attachmentId,
            MessageId = request.MessageId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            S3Key = s3Key,
            IsConfirmed = false,
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Attachments.Add(attachment);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Return proxy upload URL - the frontend uploads through the backend,
        // avoiding browser-inaccessible presigned S3 URLs.
        var uploadUrl = $"/api/v1/uploads/{attachmentId}/data";
        var downloadUrl = $"/api/v1/attachments/{attachmentId}/download";

        return new RequestUploadResponse(
            AttachmentId: attachmentId,
            UploadUrl: uploadUrl,
            DownloadUrl: downloadUrl
        );
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove path separators and limit to alphanumeric + hyphens + dots + underscores
        var sanitized = Regex.Replace(fileName, @"[/\\]", "");
        sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9\-_.]", "_");
        return sanitized;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/uploads", async (
            [FromBody] RequestUploadCommand command,
            [FromServices] RequestUploadHandler handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(command, ct))
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("RequestUpload")
            .WithTags("Uploads");
    }
}
