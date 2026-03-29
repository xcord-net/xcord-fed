using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers;

public sealed record CreateInviteCommand(
    long ServerId,
    int? MaxUses,
    DateTimeOffset? ExpiresAt,
    long? GroupId,
    long? ChannelId
);

public sealed class CreateInviteHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IRoleService roleService,
    ILogger<CreateInviteHandler> logger)
    : IRequestHandler<CreateInviteCommand, Result<InviteDto>>, IValidatable<CreateInviteCommand>
{
    public Error? Validate(CreateInviteCommand request)
    {
        if (request.MaxUses.HasValue && request.MaxUses.Value <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Max uses must be greater than 0");
        }

        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            return Error.Validation("VALIDATION_ERROR", "Expiration date must be in the future");
        }

        return null;
    }

    public async Task<Result<InviteDto>> Handle(CreateInviteCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check if server exists
        var server = await dbContext.Servers
            .FirstOrDefaultAsync(s => s.Id == request.ServerId, cancellationToken);

        if (server == null)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
        }

        // Check if user is a member of the server
        var memberCheck = await dbContext.EnsureMembership(request.ServerId, userId, cancellationToken);
        if (memberCheck.IsFailure) return memberCheck.Error;

        // Check CreateInvite permission
        var permResult = await roleService.EnsureServerRole(userId, request.ServerId, Role.CreateInvite);
        if (permResult.IsFailure)
        {
            return Error.Forbidden("MISSING_PERMISSION", "You do not have permission to create invites");
        }

        // Validate GroupId if provided
        if (request.GroupId.HasValue)
        {
            // Require ManageGroups permission to set invite auto-assignment
            var groupPermResult = await roleService.EnsureServerRole(userId, request.ServerId, Role.ManageGroups);
            if (groupPermResult.IsFailure)
            {
                return Error.Forbidden("MISSING_PERMISSION", "You need ManageGroups permission to assign a group to an invite");
            }

            // Verify the group exists in this server
            var groupExists = await dbContext.Groups
                .AsNoTracking()
                .AnyAsync(g => g.Id == request.GroupId.Value && g.ServerId == request.ServerId && !g.IsEveryone, cancellationToken);

            if (!groupExists)
            {
                return Error.NotFound("GROUP_NOT_FOUND", "The specified group was not found in this server");
            }
        }

        // Validate ChannelId if provided
        if (request.ChannelId.HasValue)
        {
            var channelExists = await dbContext.Channels
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.ChannelId.Value && c.ServerId == request.ServerId, cancellationToken);

            if (!channelExists)
            {
                return Error.NotFound("CHANNEL_NOT_FOUND", "The specified channel was not found in this server");
            }
        }

        // Generate unique 8-character alphanumeric code
        string code;
        int attempts = 0;
        do
        {
            code = GenerateInviteCode();
            attempts++;

            if (attempts > 10)
            {
                return Error.Failure("INVITE_CODE_GENERATION_FAILED", "Failed to generate unique invite code");
            }
        }
        while (await dbContext.Invites.AnyAsync(i => i.Code == code, cancellationToken));

        var now = DateTimeOffset.UtcNow;

        var invite = new Invite
        {
            Code = code,
            ServerId = request.ServerId,
            CreatedByUserId = userId,
            MaxUses = request.MaxUses,
            Uses = 0,
            ExpiresAt = request.ExpiresAt,
            GroupId = request.GroupId,
            ChannelId = request.ChannelId,
            CreatedAt = now
        };

        dbContext.Invites.Add(invite);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created invite {Code} for server {ServerId}",
            userId, code, request.ServerId);

        return new InviteDto(
            Code: invite.Code,
            ServerId: invite.ServerId,
            CreatedByUserId: invite.CreatedByUserId,
            MaxUses: invite.MaxUses,
            Uses: invite.Uses,
            ExpiresAt: invite.ExpiresAt,
            CreatedAt: invite.CreatedAt,
            GroupId: invite.GroupId,
            ChannelId: invite.ChannelId
        );
    }

    /// <summary>
    /// Generates a cryptographically secure 8-character alphanumeric invite code.
    /// </summary>
    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var result = new char[8];

        for (int i = 0; i < 8; i++)
        {
            result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }

        return new string(result);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{id:long}/invites", async (
            long id,
            CreateInviteRequest request,
            IRequestHandler<CreateInviteCommand, Result<InviteDto>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateInviteCommand(
                ServerId: id,
                MaxUses: request.MaxUses,
                ExpiresAt: request.ExpiresAt,
                GroupId: request.GroupId,
                ChannelId: request.ChannelId
            );

            return await handler.ExecuteAsync(command, ct, success => Results.Created($"/api/v1/servers/{success.ServerId}/invites/{success.Code}", success));
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateInvite")
        .WithTags("Servers", "Invites");
    }
}
