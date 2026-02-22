using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Integration tests for server-side slowmode enforcement.
/// Verifies that the Redis-backed ISlowmodeService correctly rate-limits
/// users and that SendMessageHandler returns 429 with a Retry-After header.
/// </summary>
[Collection("WebApp")]
public class SlowmodeEnforcementTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public SlowmodeEnforcementTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Core rate-limit scenario ────────────

    /// <summary>
    /// Full slowmode lifecycle:
    ///   1. Set channel slowmode to 2 seconds.
    ///   2. Send first message → 201 Created.
    ///   3. Immediately send second message → 429 with Retry-After header.
    ///   4. Wait for cooldown to expire.
    ///   5. Send again → 201 Created.
    /// </summary>
    [Fact]
    public async Task SendMessage_WithSlowmode_EnforcesRateLimit()
    {
        // Arrange — owner creates server/channel, member joins to test rate-limiting
        // (Owners are exempt from slowmode, so we must test with a regular member)
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Set slowmode to 2 seconds on the channel
        var updateResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}",
            owner.AccessToken,
            new { slowModeSeconds = 2 });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, "setting slowmode should succeed");

        // Act 1 — first message should succeed
        var firstMessage = await _helper.SendMessageAsync(member.AccessToken, conversationId, "First message in slowmode channel");
        firstMessage.GetProperty("id").ReadLong().Should().BeGreaterThan(0);

        // Act 2 — immediate second message should be rate-limited
        var rateLimitedResponse = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            member.AccessToken,
            new { content = "Second message — should be blocked" });

        rateLimitedResponse.StatusCode.Should().Be(
            HttpStatusCode.TooManyRequests,
            "a second message sent within the slowmode window should return 429");

        // Assert Retry-After header is present and positive
        rateLimitedResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues)
            .Should().BeTrue("Retry-After header must be set on 429 responses");

        var retryAfterStr = retryAfterValues!.First();
        int.TryParse(retryAfterStr, out var retryAfterSeconds).Should().BeTrue("Retry-After must be an integer");
        retryAfterSeconds.Should().BeGreaterThan(0, "Retry-After must indicate remaining wait time");

        // Also verify the response body contains the expected error code
        var errorBody = await rateLimitedResponse.ReadAsJsonAsync<JsonElement>();
        errorBody.GetProperty("title").GetString().Should().Be("SLOWMODE_RATE_LIMITED");

        // Act 3 — wait for the 2-second cooldown to expire, then send again
        await Task.Delay(TimeSpan.FromSeconds(3));

        var thirdMessage = await _helper.SendMessageAsync(
            member.AccessToken, conversationId, "Third message after cooldown");
        thirdMessage.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
    }

    // ──────────── Owner bypass ────────────

    /// <summary>
    /// Server owners (who have ManageChannels + ManageMessages permissions)
    /// are exempt from the slowmode restriction.
    /// </summary>
    [Fact]
    public async Task SendMessage_WithSlowmode_OwnerIsExempt()
    {
        // Arrange — owner creates the server and channel
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Set slowmode to 30 seconds
        var updateResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}",
            owner.AccessToken,
            new { slowModeSeconds = 30 });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Owner sends two messages in quick succession — both should succeed
        var msg1 = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "Owner message 1");
        msg1.GetProperty("id").ReadLong().Should().BeGreaterThan(0);

        var msg2 = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "Owner message 2 — not rate-limited");
        msg2.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
    }

    // ──────────── Regular member is rate-limited ────────────

    /// <summary>
    /// A regular member (no ManageMessages / ManageChannels) IS subject to slowmode.
    /// </summary>
    [Fact]
    public async Task SendMessage_WithSlowmode_RegularMemberIsRateLimited()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Set slowmode to 30 seconds
        var updateResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}",
            owner.AccessToken,
            new { slowModeSeconds = 30 });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Member sends first message — allowed
        var firstMessage = await _helper.SendMessageAsync(member.AccessToken, conversationId, "First member message");
        firstMessage.GetProperty("id").ReadLong().Should().BeGreaterThan(0);

        // Member immediately sends second message — should be rate-limited
        var rateLimitedResponse = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            member.AccessToken,
            new { content = "Second member message — blocked" });

        rateLimitedResponse.StatusCode.Should().Be(
            HttpStatusCode.TooManyRequests,
            "regular member must be rate-limited by slowmode");

        rateLimitedResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues)
            .Should().BeTrue("Retry-After header must be present");

        var retryAfterStr = retryAfterValues!.First();
        int.TryParse(retryAfterStr, out var retryAfterSeconds).Should().BeTrue();
        retryAfterSeconds.Should().BeGreaterThan(0);
    }

    // ──────────── No slowmode — no restriction ────────────

    /// <summary>
    /// When SlowModeSeconds is 0 (disabled), rapid sends are all allowed.
    /// </summary>
    [Fact]
    public async Task SendMessage_WithoutSlowmode_AllowsRapidSends()
    {
        // Arrange — channel has no slowmode (default)
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Act — send several messages in rapid succession
        var msg1 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Rapid 1");
        var msg2 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Rapid 2");
        var msg3 = await _helper.SendMessageAsync(user.AccessToken, conversationId, "Rapid 3");

        msg1.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        msg2.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        msg3.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
    }
}
