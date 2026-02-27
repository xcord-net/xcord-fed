using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record DeleteCategoryCommand(long ServerId, long CategoryId);

public sealed class DeleteCategoryHandler(
    AppDbContext dbContext,
    IPermissionService permissionService,
    ICurrentUserService currentUserService,
    ILogger<DeleteCategoryHandler> logger)
    : IRequestHandler<DeleteCategoryCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Get category
        var category = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.ServerId == request.ServerId, cancellationToken);

        if (category == null)
        {
            return Error.NotFound("CATEGORY_NOT_FOUND", "Category not found or does not belong to this server");
        }

        // Check ManageChannels permission
        var permissionResult = await permissionService.EnsureServerPermission(
            userId,
            request.ServerId,
            Permission.ManageChannels);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Soft delete category (channels in this category get CategoryId set to null via SetNull FK behavior)
        category.DeletedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} deleted category {CategoryId} in server {ServerId}",
            userId, request.CategoryId, request.ServerId);

        return true;
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapDelete("/api/v1/servers/{serverId}/categories/{categoryId}", async (
            long serverId,
            long categoryId,
            [FromServices] DeleteCategoryHandler handler,
            CancellationToken ct) =>
        {
            var command = new DeleteCategoryCommand(serverId, categoryId);
            return await handler.ExecuteAsync(command, ct, _ => Results.NoContent());
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("DeleteCategory")
        .WithTags("Categories");
    }
}
