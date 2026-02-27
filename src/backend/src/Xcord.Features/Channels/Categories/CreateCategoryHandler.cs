using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Channels;

public sealed record CreateCategoryCommand(
    long ServerId,
    string Name,
    int Position = 0
);

public sealed record CreateCategoryResponse(
    long Id,
    long ServerId,
    string Name,
    int Position,
    DateTimeOffset CreatedAt
);

public sealed record CreateCategoryRequest(
    string Name,
    int Position = 0
);

public sealed class CreateCategoryHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    IPermissionService permissionService,
    ICurrentUserService currentUserService,
    ILogger<CreateCategoryHandler> logger)
    : IRequestHandler<CreateCategoryCommand, Result<CreateCategoryResponse>>, IValidatable<CreateCategoryCommand>
{
    public Error? Validate(CreateCategoryCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("VALIDATION_ERROR", "Category name is required");
        }

        if (request.Name.Length > 100)
        {
            return Error.Validation("VALIDATION_ERROR", "Category name must not exceed 100 characters");
        }

        if (request.Position < 0)
        {
            return Error.Validation("VALIDATION_ERROR", "Position must be non-negative");
        }

        return null;
    }

    public async Task<Result<CreateCategoryResponse>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        // Check if server exists
        var serverExists = await dbContext.Servers
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.ServerId, cancellationToken);

        if (!serverExists)
        {
            return Error.NotFound("SERVER_NOT_FOUND", "Server not found");
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

        var now = DateTimeOffset.UtcNow;

        // Create Category
        var categoryId = snowflakeGenerator.NextId();
        var category = new Category
        {
            Id = categoryId,
            ServerId = request.ServerId,
            Name = request.Name,
            Position = request.Position,
            CreatedAt = now
        };

        dbContext.Categories.Add(category);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} created category {CategoryName} (ID: {CategoryId}) in server {ServerId}",
            userId, category.Name, categoryId, request.ServerId);

        return new CreateCategoryResponse(
            Id: category.Id,
            ServerId: category.ServerId,
            Name: category.Name,
            Position: category.Position,
            CreatedAt: category.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/categories", async (
            long serverId,
            [FromBody] CreateCategoryRequest request,
            [FromServices] CreateCategoryHandler handler,
            CancellationToken ct) =>
        {
            var command = new CreateCategoryCommand(
                ServerId: serverId,
                Name: request.Name,
                Position: request.Position
            );

            return await handler.ExecuteAsync(command, ct, success => Results.Created($"/api/v1/servers/{serverId}/categories/{success.Id}", success));
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateCategory")
        .WithTags("Categories");
    }
}
