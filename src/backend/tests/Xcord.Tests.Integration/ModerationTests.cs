using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class ModerationTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public ModerationTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Ban Member ────────────

    [Fact]
    public async Task BanMember_AsOwner_RemovesMemberAndCreatesBan()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/bans",
            owner.AccessToken,
            new { userId = member.UserId, reason = "Violation of rules" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ban = await response.ReadAsJsonAsync<JsonElement>();
        ban.GetProperty("userId").ReadLong().Should().Be(member.UserId);
        ban.GetProperty("serverId").ReadLong().Should().Be(serverId);
        ban.GetProperty("reason").GetString().Should().Be("Violation of rules");

        // Verify member was removed from server
        await using var db = _fixture.CreateDbContext();
        var isMember = await db.ServerMembers
            .AnyAsync(sm => sm.UserId == member.UserId && sm.ServerId == serverId);
        isMember.Should().BeFalse("banned member should be removed from server");
    }

    [Fact]
    public async Task BanMember_WithDeleteMessageDays_SoftDeletesMessages()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        // Have the member send a message first
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();
        var message = await _helper.SendMessageAsync(member.AccessToken, conversationId, "This will be deleted");
        var messageId = message.GetProperty("id").ReadLong();

        // Ban with message deletion
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/bans",
            owner.AccessToken,
            new { userId = member.UserId, reason = "Spam", deleteMessageDays = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify message was soft-deleted
        await using var db = _fixture.CreateDbContext();
        var dbMsg = await db.Messages.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == messageId);
        dbMsg.Should().NotBeNull();
        dbMsg!.DeletedAt.Should().NotBeNull("message should be soft-deleted after ban with deleteMessageDays");
    }

    [Fact]
    public async Task BanMember_AsNonOwnerWithoutPermission_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        // Add another member who doesn't have BanMembers permission
        var moderator = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(moderator.AccessToken, inviteCode);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/bans",
            moderator.AccessToken,
            new { userId = member.UserId, reason = "Attempt" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BanMember_BanSelf_Returns400()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/bans",
            owner.AccessToken,
            new { userId = owner.UserId, reason = "Self-ban" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Unban Member ────────────

    [Fact]
    public async Task UnbanMember_ExistingBan_Returns200()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        // Ban first
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/bans",
            owner.AccessToken,
            new { userId = member.UserId });

        // Unban
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/bans/{member.UserId}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify ban is actually removed - GET /bans must not list the user any more
        var bansResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/bans",
            owner.AccessToken);
        bansResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bans = await bansResponse.ReadAsJsonAsync<JsonElement>();
        var banArray = bans.EnumerateArray().ToList();
        banArray.Should().NotContain(b => b.GetProperty("userId").ReadLong() == member.UserId,
            "unbanned user must no longer appear in the ban list");
    }

    // ──────────── List Bans ────────────

    [Fact]
    public async Task ListBans_WithBannedUser_ReturnsBanList()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        // Ban the member
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/bans",
            owner.AccessToken,
            new { userId = member.UserId, reason = "Listed ban" });

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/bans",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bans = await response.ReadAsJsonAsync<JsonElement>();
        var banArray = bans.EnumerateArray().ToList();
        banArray.Should().Contain(b => b.GetProperty("userId").ReadLong() == member.UserId);
    }

    // ──────────── Timeout Member ────────────

    [Fact]
    public async Task TimeoutMember_AsOwner_CreatesTimeout()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/timeout",
            owner.AccessToken,
            new { durationMinutes = 10, reason = "Spam" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var timeout = await response.ReadAsJsonAsync<JsonElement>();
        timeout.GetProperty("userId").ReadLong().Should().Be(member.UserId);
        timeout.GetProperty("serverId").ReadLong().Should().Be(serverId);
        timeout.GetProperty("reason").GetString().Should().Be("Spam");
        timeout.TryGetProperty("expiresAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TimeoutMember_AsNonOwnerWithoutPermission_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{owner.UserId}/timeout",
            member.AccessToken,
            new { durationMinutes = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TimeoutMember_Self_Returns400()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{owner.UserId}/timeout",
            owner.AccessToken,
            new { durationMinutes = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Remove Timeout ────────────

    [Fact]
    public async Task RemoveTimeout_ExistingTimeout_Returns200()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        // Create timeout first
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/timeout",
            owner.AccessToken,
            new { durationMinutes = 10 });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK, "timeout creation should succeed");

        // Remove timeout
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/timeout",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the timeout is actually cleared - no active (non-expired) timeout should exist
        // for this member in the database after removal
        await using var db = _fixture.CreateDbContext();
        var activeTimeout = await db.Timeouts
            .Where(t => t.UserId == member.UserId && t.ServerId == serverId && t.ExpiresAt > DateTimeOffset.UtcNow)
            .FirstOrDefaultAsync();
        activeTimeout.Should().BeNull("removing a timeout must clear the active timeout record so the member can send messages again");
    }

    // ──────────── Reports ────────────

    [Fact]
    public async Task CreateReport_AsMember_Returns200()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/reports",
            member.AccessToken,
            new { reportedUserId = owner.UserId, reason = "Abusive behavior" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await response.ReadAsJsonAsync<JsonElement>();
        report.GetProperty("reportedUserId").ReadLong().Should().Be(owner.UserId);
        report.GetProperty("reason").GetString().Should().Be("Abusive behavior");
        report.GetProperty("status").GetString().Should().Be("Pending");
    }

    [Fact]
    public async Task ListReports_AsOwner_ReturnsReports()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        // Create a report
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/reports",
            member.AccessToken,
            new { reportedUserId = owner.UserId, reason = "Test report" });

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/reports",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reports = await response.ReadAsJsonAsync<JsonElement>();
        reports.EnumerateArray().ToList().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReviewReport_AsOwner_UpdatesStatus()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        // Create a report
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/reports",
            member.AccessToken,
            new { reportedUserId = owner.UserId, reason = "Review test" });
        var report = await createResponse.ReadAsJsonAsync<JsonElement>();
        var reportId = report.GetProperty("id").ReadLong();

        // Review the report (Dismissed = 3)
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{serverId}/reports/{reportId}",
            owner.AccessToken,
            new { newStatus = 3, reviewNotes = "Not a valid report" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reviewed = await response.ReadAsJsonAsync<JsonElement>();
        reviewed.GetProperty("status").GetString().Should().Be("Dismissed");
    }

    [Fact]
    public async Task ListReports_AsNonOwnerWithoutPermission_Returns403()
    {
        var (serverId, _, member) = await SetupServerWithMember();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/reports",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Audit Log ────────────

    [Fact]
    public async Task GetAuditLog_AfterBan_ContainsBanEntry()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        // Perform a ban (which should create an audit log entry)
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/bans",
            owner.AccessToken,
            new { userId = member.UserId, reason = "Audit test" });

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/audit-log",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await response.ReadAsJsonAsync<JsonElement>();
        var logArray = logs.EnumerateArray().ToList();

        logArray.Should().Contain(l =>
            l.GetProperty("actionType").GetString() == "MemberBan");
    }

    [Fact]
    public async Task GetAuditLog_AsNonOwnerWithoutPermission_Returns403()
    {
        var (serverId, _, member) = await SetupServerWithMember();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/audit-log",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Kick Member ────────────

    [Fact]
    public async Task KickMember_AsOwner_RemovesMember()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("userId").ReadLong().Should().Be(member.UserId);
        body.GetProperty("serverId").ReadLong().Should().Be(serverId);

        // Verify member was removed from server
        await using var db = _fixture.CreateDbContext();
        var isMember = await db.ServerMembers
            .AnyAsync(sm => sm.UserId == member.UserId && sm.ServerId == serverId);
        isMember.Should().BeFalse("kicked member should be removed from server");
    }

    [Fact]
    public async Task KickMember_AsNonOwnerWithoutPermission_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        // Add another member who doesn't have KickMembers permission
        var other = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(other.AccessToken, inviteCode);

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}",
            other.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task KickMember_KickSelf_Returns400()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{owner.UserId}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task KickMember_CreatesAuditLogEntry()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}",
            owner.AccessToken);

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/audit-log",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await response.ReadAsJsonAsync<JsonElement>();
        var logArray = logs.EnumerateArray().ToList();

        logArray.Should().Contain(l =>
            l.GetProperty("actionType").GetString() == "MemberKick");
    }

    // ──────────── Helper ────────────

    /// <summary>
    /// Creates an owner, a server, and adds a member. Returns (serverId, owner, member).
    /// </summary>
    private async Task<(long ServerId, AuthenticatedUser Owner, AuthenticatedUser Member)> SetupServerWithMember()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        return (serverId, owner, member);
    }
}
