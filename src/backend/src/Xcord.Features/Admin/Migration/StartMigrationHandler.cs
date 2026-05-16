using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services.Discord;

namespace Xcord.Features.Admin.Migration;

public sealed record StartMigrationCommand(
    string BotToken,
    string GuildId,
    MigrationOptions? Options
);

public sealed record StartMigrationResponse(
    long MigrationId,
    string Status,
    string GuildId,
    long ServerId,
    DateTimeOffset CreatedAt
);

public sealed class StartMigrationHandler(
    AppDbContext dbContext,
    DiscordApiClient discordClient,
    SnowflakeIdGenerator snowflakeGenerator,
    IServiceScopeFactory scopeFactory,
    ILogger<StartMigrationHandler> logger)
    : IRequestHandler<StartMigrationCommand, Result<StartMigrationResponse>>,
      IValidatable<StartMigrationCommand>
{
    public Error? Validate(StartMigrationCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.BotToken))
            return Error.Validation("VALIDATION_ERROR", "BotToken is required");

        if (string.IsNullOrWhiteSpace(request.GuildId))
            return Error.Validation("VALIDATION_ERROR", "GuildId is required");

        return null;
    }

    public async Task<Result<StartMigrationResponse>> Handle(
        StartMigrationCommand request, CancellationToken cancellationToken)
    {
        // Validate the bot token by attempting to fetch the guild
        JsonElement guild;
        try
        {
            discordClient.SetToken(request.BotToken);
            guild = await discordClient.GetGuildAsync(request.GuildId, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Error.Validation("INVALID_BOT_TOKEN", "The provided bot token is invalid");
        }
        catch (InvalidOperationException ex)
        {
            return Error.Validation("GUILD_NOT_ACCESSIBLE", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to validate Discord bot token for guild {GuildId}", request.GuildId);
            return Error.Validation("DISCORD_VALIDATION_FAILED", "Failed to connect to Discord: " + ex.Message);
        }

        // Check no active migration is already running for this instance
        var hasActiveMigration = await dbContext.DiscordMigrations
            .AnyAsync(m => m.Status == "Running" && m.DeletedAt == null, cancellationToken);

        if (hasActiveMigration)
        {
            return Error.Conflict("MIGRATION_IN_PROGRESS",
                "Another migration is already running. Cancel it before starting a new one.");
        }

        // Get the guild name from the validated response
        var guildName = guild.TryGetProperty("name", out var nameEl)
            ? nameEl.GetString() ?? "Imported Server"
            : "Imported Server";

        var now = DateTimeOffset.UtcNow;

        // Create a placeholder server for the migration
        var serverId = snowflakeGenerator.NextId();
        var server = new Server
        {
            Id = serverId,
            Name = guildName,
            OwnerId = 0, // will be set during member migration or left as system
            MemberCount = 0,
            CreatedAt = now
        };
        dbContext.Servers.Add(server);

        // Create the migration entity
        var migrationId = snowflakeGenerator.NextId();
        var options = request.Options ?? new MigrationOptions();
        var migration = new DiscordMigration
        {
            Id = migrationId,
            DiscordGuildId = request.GuildId,
            ServerId = serverId,
            Status = "Pending",
            OptionsJson = JsonSerializer.Serialize(options),
            CreatedAt = now
        };

        dbContext.DiscordMigrations.Add(migration);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Starting Discord migration {MigrationId} for guild {GuildId}",
            migrationId, request.GuildId);

        // Fire off the orchestrator in a background task with its own DI scope
        var botToken = request.BotToken;
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<DiscordMigrationOrchestrator>();
            var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var migrationToRun = await scopedDb.DiscordMigrations.FindAsync([migrationId]).ConfigureAwait(false);
            if (migrationToRun == null) return;

            await orchestrator.RunAsync(migrationToRun, botToken, CancellationToken.None).ConfigureAwait(false);
        }, CancellationToken.None);

        return new StartMigrationResponse(
            MigrationId: migrationId,
            Status: "Pending",
            GuildId: request.GuildId,
            ServerId: serverId,
            CreatedAt: now
        );
    }

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder app)
    {
        return app.MapPost("/api/v1/admin/migration/discord", async (
            StartMigrationCommand request,
            StartMigrationHandler handler,
            CancellationToken ct) =>
        {
            return await handler.ExecuteAsync(request, ct,
                success => Results.Accepted(null, success));
        })
        .RequireAuthorization(Policies.Admin)
        .Produces<StartMigrationResponse>(202)
        .WithName("StartDiscordMigration")
        .WithTags("Admin", "Migration");
    }
}
