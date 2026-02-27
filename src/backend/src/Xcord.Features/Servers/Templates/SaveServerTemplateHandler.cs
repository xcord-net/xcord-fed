using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Servers;

public sealed record SaveServerTemplateCommand(long ServerId, string Name, string? Description);
public sealed record SaveServerTemplateRequest(string Name, string? Description);

public sealed class SaveServerTemplateHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService, IPermissionService permissionService)
    : IRequestHandler<SaveServerTemplateCommand, Result<ServerTemplateResponse>>, IValidatable<SaveServerTemplateCommand>
{
    public Error? Validate(SaveServerTemplateCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return Error.Validation("VALIDATION_ERROR", "Name is required");
        if (r.Name.Length > 100) return Error.Validation("VALIDATION_ERROR", "Name must be 100 characters or less");
        return null;
    }

    public async Task<Result<ServerTemplateResponse>> Handle(SaveServerTemplateCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var perm = await permissionService.EnsureServerPermission(userId, request.ServerId, Permission.ManageServer);
        if (perm.IsFailure) return Error.Forbidden("MISSING_PERMISSIONS", "You do not have permission to manage this server");

        var channels = await dbContext.Channels.AsNoTracking()
            .Where(c => c.ServerId == request.ServerId)
            .Select(c => new { c.Name, Type = c.Type.ToString(), c.Position })
            .ToListAsync(ct);

        var roles = await dbContext.Roles.AsNoTracking()
            .Where(r => r.ServerId == request.ServerId)
            .Select(r => new { r.Name, r.Color, Permissions = r.Permissions.ToString() })
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var template = new ServerTemplate
        {
            Id = snowflakeGenerator.NextId(), SourceServerId = request.ServerId,
            Name = request.Name, Description = request.Description,
            ChannelData = JsonSerializer.Serialize(channels),
            RoleData = JsonSerializer.Serialize(roles),
            CreatedAt = now
        };

        dbContext.ServerTemplates.Add(template);
        await dbContext.SaveChangesAsync(ct);

        return new ServerTemplateResponse(template.Id, template.Name, template.Description,
            template.SourceServerId, template.ChannelData, template.RoleData, template.UsageCount, template.CreatedAt);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/servers/{serverId}/templates", async (
            long serverId, SaveServerTemplateRequest request,
            IRequestHandler<SaveServerTemplateCommand, Result<ServerTemplateResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new SaveServerTemplateCommand(serverId, request.Name, request.Description), ct))
        .RequireAuthorization(Policies.User)
        .WithName("SaveServerTemplate").WithTags("ServerTemplates");
}
