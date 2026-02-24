using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Webhooks;

public sealed record CreateWebhookCommand(
    long ServerId,
    long ChannelId,
    string Name,
    string? AvatarUrl
);

public sealed record CreateWebhookResponse(
    long Id,
    string Token,
    long ChannelId,
    string Name,
    string? AvatarUrl,
    long CreatedByUserId,
    DateTimeOffset CreatedAt,
    string Url
);

public sealed class CreateWebhookHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IHttpContextAccessor httpContextAccessor,
    IPermissionService permissionService,
    ILogger<CreateWebhookHandler> logger,
    IOptions<InstanceOptions> instanceOptions)
    : IRequestHandler<CreateWebhookCommand, Result<CreateWebhookResponse>>, IValidatable<CreateWebhookCommand>
{
    private readonly string _instanceDomain = instanceOptions.Value.Domain;

    public Error? Validate(CreateWebhookCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be positive");
        }

        if (request.ChannelId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ChannelId must be positive");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("VALIDATION_ERROR", "Name is required");
        }

        if (request.Name.Length > 80)
        {
            return Error.Validation("VALIDATION_ERROR", "Name must be 80 characters or less");
        }

        if (request.AvatarUrl != null && request.AvatarUrl.Length > 512)
        {
            return Error.Validation("VALIDATION_ERROR", "AvatarUrl must be 512 characters or less");
        }

        return null;
    }

    public async Task<Result<CreateWebhookResponse>> Handle(CreateWebhookCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

        // Verify server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Verify channel exists and belongs to server
        var channel = await dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel == null)
        {
            return Error.NotFound("CHANNEL_NOT_FOUND", "Channel not found");
        }

        if (channel.ServerId != request.ServerId)
        {
            return Error.Validation("CHANNEL_SERVER_MISMATCH", "Channel does not belong to this server");
        }

        // Check ManageWebhooks permission
        var permissionResult = await permissionService.EnsureChannelPermission(
            userId,
            request.ChannelId,
            Permission.ManageWebhooks);

        if (permissionResult.IsFailure)
        {
            return Error.Forbidden(
                "MISSING_PERMISSIONS",
                "You do not have permission to manage webhooks in this channel");
        }

        // Generate a random webhook token (64 bytes, URL-safe base64)
        var tokenBytes = new byte[64];
        RandomNumberGenerator.Fill(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        // Create webhook
        var now = DateTimeOffset.UtcNow;
        var webhookId = snowflakeGenerator.NextId();
        var webhook = new Webhook
        {
            Id = webhookId,
            Token = token,
            ChannelId = request.ChannelId,
            Name = request.Name,
            AvatarUrl = request.AvatarUrl,
            CreatedByUserId = userId,
            CreatedAt = now
        };

        dbContext.Webhooks.Add(webhook);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created webhook {WebhookId} in channel {ChannelId}",
            userId, webhookId, request.ChannelId);

        var scheme = httpContextAccessor.HttpContext?.Request.Scheme ?? "https";
        var webhookUrl = $"{scheme}://{_instanceDomain}/api/v1/webhooks/{webhookId}/{token}";

        return new CreateWebhookResponse(
            Id: webhook.Id,
            Token: token,
            ChannelId: webhook.ChannelId,
            Name: webhook.Name,
            AvatarUrl: webhook.AvatarUrl,
            CreatedByUserId: userId,
            CreatedAt: webhook.CreatedAt,
            Url: webhookUrl
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/webhooks", async (
            long serverId,
            CreateWebhookRequest request,
            IRequestHandler<CreateWebhookCommand, Result<CreateWebhookResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateWebhookCommand(
                ServerId: serverId,
                ChannelId: request.ChannelId,
                Name: request.Name,
                AvatarUrl: request.AvatarUrl
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateWebhook")
        .WithTags("Webhooks");
    }
}

public sealed record CreateWebhookRequest(
    long ChannelId,
    string Name,
    string? AvatarUrl
);
