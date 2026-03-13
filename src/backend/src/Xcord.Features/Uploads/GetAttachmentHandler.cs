using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Uploads;

public sealed record GetAttachmentCommand(
    long AttachmentId
);

public sealed record GetAttachmentResponse(
    long Id,
    long? MessageId,
    string FileName,
    string ContentType,
    long FileSize,
    int? Width,
    int? Height,
    string DownloadUrl,
    string? ThumbnailUrl,
    DateTimeOffset CreatedAt
);

public sealed class GetAttachmentHandler(
    AppDbContext dbContext,
    IStorageService storageService,
    ICurrentUserService currentUserService,
    IConversationResolver conversationResolver)
    : IRequestHandler<GetAttachmentCommand, Result<GetAttachmentResponse>>
{
    public async Task<Result<GetAttachmentResponse>> Handle(GetAttachmentCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get attachment with its associated message (needed for ConversationId)
        var attachment = await dbContext.Attachments
            .AsNoTracking()
            .Include(a => a.Message)
            .FirstOrDefaultAsync(a => a.Id == request.AttachmentId, cancellationToken);

        if (attachment == null)
        {
            return Error.NotFound("ATTACHMENT_NOT_FOUND", "Attachment not found");
        }

        if (attachment.Message == null)
        {
            return Error.NotFound("MESSAGE_NOT_FOUND", "Associated message not found");
        }

        // Verify the requesting user has access to the conversation containing this attachment.
        // ReadMessageHistory permission is sufficient - if you can read the channel you can see attachments.
        var contextResult = await conversationResolver.ResolveAsync(
            attachment.Message.ConversationId, userId, Permission.ReadMessageHistory, cancellationToken);
        if (contextResult.IsFailure) return contextResult.Error;

        // Generate pre-signed download URL (1 hour expiry)
        var downloadUrl = await storageService.GenerateDownloadUrlAsync(attachment.S3Key, TimeSpan.FromHours(1));

        // Generate pre-signed thumbnail URL if a thumbnail has been generated.
        // Empty string is a sentinel meaning "not applicable" (non-image attachment).
        string? thumbnailUrl = null;
        if (!string.IsNullOrEmpty(attachment.ThumbnailS3Key))
        {
            thumbnailUrl = await storageService.GenerateDownloadUrlAsync(attachment.ThumbnailS3Key, TimeSpan.FromHours(1));
        }

        return new GetAttachmentResponse(
            Id: attachment.Id,
            MessageId: attachment.MessageId,
            FileName: attachment.FileName,
            ContentType: attachment.ContentType,
            FileSize: attachment.FileSize,
            Width: attachment.Width,
            Height: attachment.Height,
            DownloadUrl: downloadUrl,
            ThumbnailUrl: thumbnailUrl,
            CreatedAt: attachment.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapGet("/api/v1/attachments/{attachmentId}", async (
            long attachmentId,
            [FromServices] GetAttachmentHandler handler,
            CancellationToken ct) =>
        {
            var command = new GetAttachmentCommand(attachmentId);
            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("GetAttachment")
        .WithTags("Uploads");
    }
}
