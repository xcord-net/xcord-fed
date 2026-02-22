using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels.Categories;

public sealed record UpdateCategoryCommand(
    long ServerId,
    long CategoryId,
    string? Name = null,
    int? Position = null
);

public sealed record UpdateCategoryResponse(
    long Id,
    long ServerId,
    string Name,
    int Position,
    DateTimeOffset CreatedAt
);

public sealed record UpdateCategoryRequest(
    string? Name = null,
    int? Position = null
);

public sealed class UpdateCategoryHandler(
    AppDbContext dbContext,
    IPermissionService permissionService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<UpdateCategoryHandler> logger)
    : IRequestHandler<UpdateCategoryCommand, Result<UpdateCategoryResponse>>, IValidatable<UpdateCategoryCommand>
{
    public Error? Validate(UpdateCategoryCommand request)
    {
        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Error.Validation("VALIDATION_ERROR", "Category name cannot be empty");
            }

            if (request.Name.Length > 100)
            {
                return Error.Validation("VALIDATION_ERROR", "Category name must not exceed 100 characters");
            }
        }

        if (request.Position.HasValue && request.Position.Value < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Position must be non-negative");
        }

        return null;
    }

    public async Task<Result<UpdateCategoryResponse>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        // Get current user ID from JWT claims
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");
        }

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

        // Apply updates
        if (request.Name != null) category.Name = request.Name;
        if (request.Position.HasValue) category.Position = request.Position.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} updated category {CategoryId} in server {ServerId}",
            userId, request.CategoryId, request.ServerId);

        return new UpdateCategoryResponse(
            Id: category.Id,
            ServerId: category.ServerId,
            Name: category.Name,
            Position: category.Position,
            CreatedAt: category.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/servers/{serverId}/categories/{categoryId}", async (
            long serverId,
            long categoryId,
            [FromBody] UpdateCategoryRequest request,
            [FromServices] UpdateCategoryHandler handler,
            CancellationToken ct) =>
        {
            var command = new UpdateCategoryCommand(
                ServerId: serverId,
                CategoryId: categoryId,
                Name: request.Name,
                Position: request.Position
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("UpdateCategory")
        .WithTags("Categories");
    }
}
