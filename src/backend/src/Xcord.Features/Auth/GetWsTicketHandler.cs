using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Cryptography;
using Xcord.Infrastructure.Services;

using Xcord.Features.Authorization;
using Xcord.Infrastructure.Options;

namespace Xcord.Features.Auth;

public sealed record GetWsTicketRequest(long UserId);

public sealed class GetWsTicketHandler(
    IConnectionMultiplexer redis,
    IOptions<RedisOptions> redisOptions)
    : IRequestHandler<GetWsTicketRequest, Result<string>>
{
    private readonly string _channelPrefix = redisOptions.Value.ChannelPrefix;

    public async Task<Result<string>> Handle(GetWsTicketRequest request, CancellationToken cancellationToken)
    {
        // Generate a random 32-byte ticket
        var ticketBytes = new byte[32];
        RandomNumberGenerator.Fill(ticketBytes);
        var ticket = Convert.ToBase64String(ticketBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        // Store in Redis with 5 minute TTL
        var db = redis.GetDatabase();
        var ticketKey = $"{_channelPrefix}:wsticket:{ticket}";
        await db.StringSetAsync(ticketKey, request.UserId.ToString(), TimeSpan.FromMinutes(5)).ConfigureAwait(false);

        return Result<string>.Success(ticket);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/auth/ws-ticket", async (
                [FromServices] ICurrentUserService currentUserService,
                [FromServices] GetWsTicketHandler handler,
                CancellationToken ct) =>
            {
                var userIdResult = currentUserService.GetCurrentUserId();
                if (userIdResult.IsFailure)
                    return Results.Problem(statusCode: userIdResult.Error.StatusCode, title: userIdResult.Error.Code, detail: userIdResult.Error.Message);
                var userId = userIdResult.Value;

                return await handler.ExecuteAsync(new GetWsTicketRequest(userId), ct, success => Results.Ok(new { ticket = success })).ConfigureAwait(false);
            })
            .RequireAnyAuthorization(Policies.User, Policies.Bot)
            .WithName("GetWsTicket")
            .WithTags("Auth");
    }
}
