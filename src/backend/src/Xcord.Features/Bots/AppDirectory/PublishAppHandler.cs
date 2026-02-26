using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

namespace Xcord.Features.Bots.AppDirectory;

public sealed record PublishAppCommand(long BotTokenId, string Name, string? Description, string? ShortDescription, string? IconUrl, string? Category, string? Tags);
public sealed record PublishAppRequest(long BotTokenId, string Name, string? Description, string? ShortDescription, string? IconUrl, string? Category, string? Tags);

public sealed class PublishAppHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService)
    : IRequestHandler<PublishAppCommand, Result<AppDetailResponse>>, IValidatable<PublishAppCommand>
{
    public Error? Validate(PublishAppCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return Error.Validation("VALIDATION_ERROR", "Name is required");
        if (r.Name.Length > 100) return Error.Validation("VALIDATION_ERROR", "Name must be 100 characters or less");
        return null;
    }

    public async Task<Result<AppDetailResponse>> Handle(PublishAppCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;

        var exists = await dbContext.AppListings.AsNoTracking().AnyAsync(a => a.BotTokenId == request.BotTokenId, ct);
        if (exists) return Error.Conflict("ALREADY_PUBLISHED", "This bot already has a listing");

        var now = DateTimeOffset.UtcNow;
        var listing = new AppListing
        {
            Id = snowflakeGenerator.NextId(), BotTokenId = request.BotTokenId,
            Name = request.Name, Description = request.Description, ShortDescription = request.ShortDescription,
            IconUrl = request.IconUrl, Category = request.Category, Tags = request.Tags,
            IsPublished = true, CreatedAt = now
        };
        dbContext.AppListings.Add(listing);
        await dbContext.SaveChangesAsync(ct);

        return new AppDetailResponse(listing.Id, listing.Name, listing.Description, listing.ShortDescription,
            listing.IconUrl, listing.Category, listing.Tags, 0, false, now, null, 0);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/app-directory", async (
            PublishAppRequest request,
            IRequestHandler<PublishAppCommand, Result<AppDetailResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new PublishAppCommand(request.BotTokenId, request.Name, request.Description,
                request.ShortDescription, request.IconUrl, request.Category, request.Tags), ct))
        .RequireAuthorization(Policies.User)
        .WithName("PublishApp").WithTags("AppDirectory");
}
