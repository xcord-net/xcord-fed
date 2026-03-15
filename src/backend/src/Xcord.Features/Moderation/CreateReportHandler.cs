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

public sealed record CreateReportCommand(
    long ServerId,
    long? ReportedUserId,
    long? ReportedMessageId,
    string Reason
);

public sealed record CreateReportResponse(
    long Id,
    long ServerId,
    long ReporterId,
    long? ReportedUserId,
    long? ReportedMessageId,
    string Reason,
    ReportStatus Status,
    DateTimeOffset CreatedAt
);

public sealed class CreateReportHandler(
    AppDbContext dbContext,
    SnowflakeIdGenerator snowflakeGenerator,
    ICurrentUserService currentUserService,
    IOutboxWriter outboxWriter,
    ILogger<CreateReportHandler> logger)
    : IRequestHandler<CreateReportCommand, Result<CreateReportResponse>>, IValidatable<CreateReportCommand>
{
    public Error? Validate(CreateReportCommand request)
    {
        if (request.ServerId <= 0)
        {
            return Error.Validation("VALIDATION_ERROR", "ServerId must be greater than 0");
        }

        if (string.IsNullOrEmpty(request.Reason))
        {
            return Error.Validation("VALIDATION_ERROR", "Reason is required");
        }

        if (request.Reason.Length > 1000)
        {
            return Error.Validation("VALIDATION_ERROR", "Reason cannot exceed 1000 characters");
        }

        if (!request.ReportedUserId.HasValue && !request.ReportedMessageId.HasValue)
        {
            return Error.Validation("VALIDATION_ERROR", "Either ReportedUserId or ReportedMessageId must be provided");
        }

        return null;
    }

    public async Task<Result<CreateReportResponse>> Handle(CreateReportCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetCurrentUserId();
        if (userIdResult.IsFailure) return userIdResult.Error;
        var reporterId = userIdResult.Value;

        // Verify reporter is a member of the server
        var isReporterMember = await dbContext.ServerMembers
            .AnyAsync(sm => sm.UserId == reporterId && sm.ServerId == request.ServerId, cancellationToken);

        if (!isReporterMember)
        {
            return Error.Forbidden("NOT_MEMBER", "You must be a member of this server to submit reports");
        }

        // If ReportedMessageId is provided, verify it exists and belongs to this server
        if (request.ReportedMessageId.HasValue)
        {
            var messageExists = await dbContext.Messages
                .AsNoTracking()
                .Where(m => m.Id == request.ReportedMessageId.Value)
                .Join(
                    dbContext.Channels.Where(c => c.ServerId == request.ServerId),
                    m => m.ConversationId,
                    c => c.ConversationId,
                    (m, c) => m)
                .AnyAsync(cancellationToken);

            if (!messageExists)
            {
                return Error.NotFound("MESSAGE_NOT_FOUND", "Message not found in this server");
            }
        }

        var now = DateTimeOffset.UtcNow;

        // Create report
        var reportId = snowflakeGenerator.NextId();
        var report = new Report
        {
            Id = reportId,
            ServerId = request.ServerId,
            ReporterId = reporterId,
            ReportedUserId = request.ReportedUserId,
            ReportedMessageId = request.ReportedMessageId,
            Reason = request.Reason,
            Status = ReportStatus.Pending,
            CreatedAt = now
        };

        dbContext.Reports.Add(report);

        // Create audit log
        dbContext.AuditLogs.AddEntry(
            snowflakeGenerator,
            serverId: request.ServerId,
            actorId: reporterId,
            actionType: "report.create",
            targetId: request.ReportedUserId ?? request.ReportedMessageId ?? 0L,
            reason: request.Reason,
            createdAt: now);

        // Write outbox event
        await outboxWriter.WriteAsync(dbContext, "Report.Created", new
        {
            ServerId = request.ServerId,
            ReportId = reportId,
            ReporterId = reporterId
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {ReporterId} created report {ReportId} in server {ServerId}",
            reporterId, reportId, request.ServerId);

        return new CreateReportResponse(
            Id: report.Id,
            ServerId: report.ServerId,
            ReporterId: report.ReporterId,
            ReportedUserId: report.ReportedUserId,
            ReportedMessageId: report.ReportedMessageId,
            Reason: report.Reason,
            Status: report.Status,
            CreatedAt: report.CreatedAt
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/servers/{serverId}/reports", async (
            long serverId,
            CreateReportRequest requestBody,
            IRequestHandler<CreateReportCommand, Result<CreateReportResponse>> handler,
            CancellationToken ct) =>
        {
            var command = new CreateReportCommand(
                ServerId: serverId,
                ReportedUserId: requestBody.ReportedUserId,
                ReportedMessageId: requestBody.ReportedMessageId,
                Reason: requestBody.Reason
            );

            return await handler.ExecuteAsync(command, ct);
        })
        .RequireAnyAuthorization(Policies.User, Policies.Bot)
        .WithName("CreateReport")
        .WithTags("Moderation");
    }
}

public sealed record CreateReportRequest(
    long? ReportedUserId,
    long? ReportedMessageId,
    string Reason
);
