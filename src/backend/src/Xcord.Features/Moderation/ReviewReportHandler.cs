using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Features.Authorization;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Xcord.Features.Moderation;

public sealed record ReviewReportCommand(
    long ServerId,
    long ReportId,
    ReportStatus NewStatus,
    string? ReviewNotes
);

public sealed record ReviewReportResponse(
    long Id,
    ReportStatus Status,
    long ReviewedById,
    string? ReviewNotes
);

public sealed class ReviewReportHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    SnowflakeIdGenerator snowflakeGenerator,
    ILogger<ReviewReportHandler> logger)
    : IRequestHandler<ReviewReportCommand, Result<ReviewReportResponse>>, IValidatable<ReviewReportCommand>
{
    public Error? Validate(ReviewReportCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be greater than 0");
        }

        if (request.ReportId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ReportId must be greater than 0");
        }

        if (request.NewStatus == ReportStatus.Pending)
        {
            return Error.Validation("VALIDATION_ERROR", "NewStatus must be Reviewed, ActionTaken, or Dismissed");
        }

        if (!string.IsNullOrEmpty(request.ReviewNotes) && request.ReviewNotes.Length > 1000)
        {
            return Error.Validation("VALIDATION_ERROR", "ReviewNotes cannot exceed 1000 characters");
        }

        return null;
    }

    public async Task<Result<ReviewReportResponse>> Handle(ReviewReportCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var reviewerId = userIdResult.Value;

        // Check if user has ManageReports permission
        var permissionResult = await permissionService.EnsureServerPermission(
            reviewerId,
            request.ServerId,
            Permission.ManageReports);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        // Find the report
        var report = await dbContext.Reports
            .FirstOrDefaultAsync(r => r.Id == request.ReportId && r.ServerId == request.ServerId && r.DeletedAt == null, cancellationToken);

        if (report == null)
        {
            return Error.NotFound("REPORT_NOT_FOUND", "Report not found");
        }

        var now = DateTimeOffset.UtcNow;

        // Update report
        report.Status = request.NewStatus;
        report.ReviewedById = reviewerId;
        report.ReviewNotes = request.ReviewNotes;

        // Create audit log
        var auditLogId = snowflakeGenerator.NextId();
        var auditLog = new AuditLog
        {
            Id = auditLogId,
            ServerId = request.ServerId,
            ActorId = reviewerId,
            ActionType = "report.review",
            TargetId = request.ReportId,
            Reason = $"Status changed to {request.NewStatus}",
            CreatedAt = now
        };

        dbContext.AuditLogs.Add(auditLog);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {ReviewerId} reviewed report {ReportId} in server {ServerId}, new status: {Status}",
            reviewerId, request.ReportId, request.ServerId, request.NewStatus);

        return new ReviewReportResponse(
            Id: report.Id,
            Status: report.Status,
            ReviewedById: reviewerId,
            ReviewNotes: report.ReviewNotes
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPatch("/api/v1/servers/{serverId}/reports/{reportId}", async (
            long serverId,
            long reportId,
            ReviewReportRequest requestBody,
            IRequestHandler<ReviewReportCommand, Result<ReviewReportResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new ReviewReportCommand(
                ServerId: serverId,
                ReportId: reportId,
                NewStatus: requestBody.NewStatus,
                ReviewNotes: requestBody.ReviewNotes
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("ReviewReport")
        .WithTags("Moderation");
    }
}

public sealed record ReviewReportRequest(
    ReportStatus NewStatus,
    string? ReviewNotes
);
