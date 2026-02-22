using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;

namespace Xcord.Features.Servers.Templates;

public sealed record DeleteServerTemplateCommand(long TemplateId);
public sealed record DeleteServerTemplateResponse(bool Deleted);

public sealed class DeleteServerTemplateHandler(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<DeleteServerTemplateCommand, Result<DeleteServerTemplateResponse>>
{
    public async Task<Result<DeleteServerTemplateResponse>> Handle(DeleteServerTemplateCommand request, CancellationToken ct)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out _))
            return Error.Forbidden("UNAUTHORIZED", "User is not authenticated");

        var template = await dbContext.ServerTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, ct);
        if (template == null) return Error.NotFound("TEMPLATE_NOT_FOUND", "Template not found");

        template.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return new DeleteServerTemplateResponse(true);
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/server-templates/{templateId}", async (
            long templateId,
            IRequestHandler<DeleteServerTemplateCommand, Result<DeleteServerTemplateResponse>> handler,
            CancellationToken ct) =>
            await handler.ExecuteAsync(new DeleteServerTemplateCommand(templateId), ct))
        .RequireAuthorization(Policies.User)
        .WithName("DeleteServerTemplate").WithTags("ServerTemplates");
}
