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

public sealed record CreateFromTemplateCommand(long TemplateId, string ServerName);
public sealed record CreateFromTemplateRequest(long TemplateId, string ServerName);
public sealed record CreateFromTemplateResponse(long Id, string Name);

public sealed class CreateFromTemplateHandler(
    AppDbContext dbContext, SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateFromTemplateCommand, Result<CreateFromTemplateResponse>>, IValidatable<CreateFromTemplateCommand>
{
    public Error? Validate(CreateFromTemplateCommand r)
    {
        if (string.IsNullOrWhiteSpace(r.ServerName)) return Error.Validation("VALIDATION_ERROR", "Server name is required");
        if (r.ServerName.Length > 100) return Error.Validation("VALIDATION_ERROR", "Server name must be 100 characters or less");
        return null;
    }

    public async Task<Result<CreateFromTemplateResponse>> Handle(CreateFromTemplateCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var userId = userIdResult.Value;

        var template = await dbContext.ServerTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, ct).ConfigureAwait(false);
        if (template == null) return Error.NotFound("TEMPLATE_NOT_FOUND", "Template not found");

        var now = DateTimeOffset.UtcNow;
        var conversationId = snowflakeGenerator.NextId();
        var conversation = new Conversation { Id = conversationId, Type = ConversationType.Channel };
        dbContext.Conversations.Add(conversation);

        var server = new Server
        {
            Id = snowflakeGenerator.NextId(), Name = request.ServerName, OwnerId = userId,
            MemberCount = 1, CreatedAt = now
        };
        dbContext.Servers.Add(server);

        var member = new ServerMember { ServerId = server.Id, UserId = userId, JoinedAt = now };
        dbContext.ServerMembers.Add(member);

        var defaultChannel = new Channel
        {
            Id = snowflakeGenerator.NextId(), ConversationId = conversationId, ServerId = server.Id,
            Name = "general", Type = ChannelType.Text, Position = 0, CreatedAt = now
        };
        dbContext.Channels.Add(defaultChannel);

        template.UsageCount++;
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return new CreateFromTemplateResponse(server.Id, server.Name);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/servers/from-template", async (
            CreateFromTemplateRequest request,
            IRequestHandler<CreateFromTemplateCommand, Result<CreateFromTemplateResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new CreateFromTemplateCommand(request.TemplateId, request.ServerName), ct, success => Results.Created($"/api/v1/servers/{success.Id}", success)))
        .RequireAuthorization(Policies.User)
        .WithName("CreateFromTemplate").WithTags("ServerTemplates");
}
