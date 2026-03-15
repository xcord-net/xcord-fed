using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord.Shared.Extensions;

namespace Xcord.Features.Servers;

public sealed record DeleteServerTemplateCommand(long TemplateId);
public sealed record DeleteServerTemplateResponse(bool Deleted);

public sealed class DeleteServerTemplateHandler(AppDbContext dbContext, ICurrentUserService currentUserService)
    : IRequestHandler<DeleteServerTemplateCommand, Result<DeleteServerTemplateResponse>>
{
    public async Task<Result<DeleteServerTemplateResponse>> Handle(DeleteServerTemplateCommand request, CancellationToken ct)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;

        var template = await dbContext.ServerTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, ct);
        if (template == null) return Error.NotFound("TEMPLATE_NOT_FOUND", "Template not found");

        template.SoftDelete();
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
