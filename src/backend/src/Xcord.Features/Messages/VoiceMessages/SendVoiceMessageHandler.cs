using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Messages.VoiceMessages;

public sealed record SendVoiceMessageCommand(long ConversationId, string AudioData, int DurationMs, string? WaveformData);
public sealed record SendVoiceMessageRequest(string AudioData, int DurationMs, string? WaveformData);
public sealed record VoiceMessageResponse(long Id, long ConversationId, long AuthorId, string Type, long? AttachmentId, DateTimeOffset CreatedAt);

public sealed class SendVoiceMessageHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<SendVoiceMessageCommand, Result<VoiceMessageResponse>>, IValidatable<SendVoiceMessageCommand>
{
    public Error? Validate(SendVoiceMessageCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.AudioData)) return Error.Validation("VALIDATION_ERROR", "Audio data is required");
        if (r.DurationMs <= 0) return Error.Validation("VALIDATION_ERROR", "Duration must be positive");
        if (r.DurationMs > 300000) return Error.Validation("VALIDATION_ERROR", "Voice message cannot exceed 5 minutes");
        return null;
    }

    public async Task<Result<VoiceMessageResponse>> Handle(SendVoiceMessageCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var conversation = await dbContext.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct);
        if (conversation == null) return Error.NotFound("CONVERSATION_NOT_FOUND", "Conversation not found");

        var now = DateTimeOffset.UtcNow;
        var messageId = snowflakeGenerator.NextId();
        var attachmentId = snowflakeGenerator.NextId();

        var attachment = new Attachment
        {
            Id = attachmentId, MessageId = messageId, FileName = "voice-message.ogg",
            ContentType = "audio/ogg", FileSize = request.AudioData.Length,
            S3Key = $"voice-messages/{messageId}.ogg",
            CreatedAt = now
        };
        dbContext.Attachments.Add(attachment);

        var message = new Message
        {
            Id = messageId, ConversationId = request.ConversationId, AuthorId = userId,
            Content = "", Type = MessageType.VoiceMessage,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { request.DurationMs, request.WaveformData }),
            CreatedAt = now
        };
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(ct);

        return new VoiceMessageResponse(message.Id, message.ConversationId, userId, "VoiceMessage", attachmentId, now);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/conversations/{conversationId}/voice-messages", async (
            long conversationId, SendVoiceMessageRequest request,
            IRequestHandler<SendVoiceMessageCommand, Result<VoiceMessageResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new SendVoiceMessageCommand(conversationId, request.AudioData, request.DurationMs, request.WaveformData), ct))
        .RequireAuthorization(Policies.User)
        .WithName("SendVoiceMessage").WithTags("VoiceMessages");
}
