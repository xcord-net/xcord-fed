using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;

namespace Xcord.Infrastructure.Services.Discord.Phases;

/// <summary>
/// Phase 2 - Members: paginate Discord guild members, create placeholder
/// User rows, and link them to the server with their role assignments.
/// </summary>
public sealed class MembersPhase : IMigrationPhase
{
    private readonly MigrationPhaseContext _ctx;

    public MembersPhase(MigrationPhaseContext ctx)
    {
        _ctx = ctx;
    }

    public string PhaseName => "Members";

    public async Task RunAsync(DiscordMigration migration, MigrationOptions options, CancellationToken ct)
    {
        if (MigrationPhaseContext.IsPhaseDone(migration, PhaseName))
            return;

        var dbContext = _ctx.DbContext;
        var client = _ctx.Client;
        var snowflakeGenerator = _ctx.SnowflakeGenerator;
        var logger = _ctx.Logger;

        migration.CurrentPhase = PhaseName;
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var guildId = migration.DiscordGuildId;

        // Check if migration was cancelled
        if (await _ctx.IsStoppedAsync(migration.Id, ct)) return;

        var groupLookup = await dbContext.DiscordIdMappings
            .Where(m => m.MigrationId == migration.Id && m.EntityType == "Group")
            .ToDictionaryAsync(m => m.DiscordId, m => m.XcordId, ct);

        // Determine last paginated user ID from checkpoint
        var checkpoint = MigrationPhaseContext.LoadCheckpoint(migration);

        string? afterMemberId = checkpoint.LastMemberDiscordId;
        int totalMigrated = migration.MigratedMembers;

        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (await _ctx.IsStoppedAsync(migration.Id, ct)) break;

            var membersPage = await client.GetGuildMembersAsync(guildId, limit: 1000, after: afterMemberId, ct: ct).ConfigureAwait(false);
            var memberArray = membersPage.EnumerateArray().ToList();

            if (memberArray.Count == 0) break;

            var userBatch = new List<User>();
            var memberBatch = new List<ServerMember>();
            var memberGroupBatch = new List<MemberGroup>();
            var userMappings = new List<DiscordIdMapping>();

            foreach (var member in memberArray)
            {
                var userObj = member.GetProperty("user");
                var discordUserId = userObj.GetProperty("id").GetString()!;
                var username = userObj.GetProperty("username").GetString() ?? "user";
                var displayName = member.TryGetProperty("nick", out var nickEl) && nickEl.ValueKind != JsonValueKind.Null
                    ? nickEl.GetString() ?? username
                    : (userObj.TryGetProperty("global_name", out var gnEl) && gnEl.ValueKind != JsonValueKind.Null
                        ? gnEl.GetString() ?? username
                        : username);

                var joinedAtStr = member.TryGetProperty("joined_at", out var joinedEl)
                    ? joinedEl.GetString() : null;
                var joinedAt = joinedAtStr != null && DateTimeOffset.TryParse(joinedAtStr, out var jd) ? jd : now;

                // Check if user already exists from a previous checkpoint page
                if (await dbContext.DiscordIdMappings
                    .AnyAsync(m => m.MigrationId == migration.Id
                        && m.EntityType == "User"
                        && m.DiscordId == discordUserId, ct))
                {
                    continue;
                }

                var userId = snowflakeGenerator.NextId();

                // Generate placeholder email - not valid but satisfies the schema
                var placeholderEmail = $"discord-{discordUserId}@migration.local";
                var emailBytes = System.Text.Encoding.UTF8.GetBytes(placeholderEmail);
                var hashBytes = System.Security.Cryptography.SHA256.HashData(emailBytes);

                var user = new User
                {
                    Id = userId,
                    Username = MigrationPhaseContext.SanitizeUsername(username),
                    DisplayName = displayName[..Math.Min(displayName.Length, 32)],
                    Email = emailBytes,
                    EmailHash = hashBytes,
                    PasswordHash = "$2a$12$migrated-account-placeholder-hash",
                    IsDisabled = false,
                    EmailConfirmed = false,
                    CreatedAt = now
                };
                userBatch.Add(user);

                var serverMember = new ServerMember
                {
                    UserId = userId,
                    ServerId = migration.ServerId,
                    JoinedAt = joinedAt
                };
                memberBatch.Add(serverMember);

                // Role assignments
                if (member.TryGetProperty("roles", out var rolesEl))
                {
                    foreach (var roleIdEl in rolesEl.EnumerateArray())
                    {
                        var discordRoleId = roleIdEl.GetString()!;
                        if (groupLookup.TryGetValue(discordRoleId, out var groupId))
                        {
                            memberGroupBatch.Add(new MemberGroup
                            {
                                UserId = userId,
                                ServerId = migration.ServerId,
                                GroupId = groupId
                            });
                        }
                    }
                }

                userMappings.Add(new DiscordIdMapping
                {
                    Id = snowflakeGenerator.NextId(),
                    MigrationId = migration.Id,
                    DiscordId = discordUserId,
                    XcordId = userId,
                    EntityType = "User"
                });

                afterMemberId = discordUserId;
                totalMigrated++;
            }

            if (userBatch.Count > 0)
            {
                dbContext.Users.AddRange(userBatch);
                dbContext.ServerMembers.AddRange(memberBatch);
                dbContext.MemberGroups.AddRange(memberGroupBatch);
                dbContext.DiscordIdMappings.AddRange(userMappings);
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
            }

            // Save checkpoint
            checkpoint.LastMemberDiscordId = afterMemberId;
            migration.CheckpointJson = JsonSerializer.Serialize(checkpoint);
            migration.MigratedMembers = totalMigrated;
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            if (memberArray.Count < 1000) break;
        }

        dbContext.ChangeTracker.AutoDetectChangesEnabled = true;

        migration.TotalMembers = totalMigrated;
        migration.MigratedMembers = totalMigrated;
        MigrationPhaseContext.SetPhaseDone(migration, PhaseName);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Migration {MigrationId} Phase 2 complete: {MemberCount} members",
            migration.Id, totalMigrated);
    }
}
