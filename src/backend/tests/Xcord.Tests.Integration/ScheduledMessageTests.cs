using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class ScheduledMessageTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public ScheduledMessageTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helpers ────────────

    private sealed record ScheduledMessageTestContext(
        AuthenticatedUser Owner,
        long ServerId,
        long ChannelId,
        long ConversationId);

    private async Task<ScheduledMessageTestContext> SetupAsync()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();
        var conversationId = channel.GetProperty("conversationId").ReadLong();
        return new ScheduledMessageTestContext(owner, serverId, channelId, conversationId);
    }

    // ──────────── Create Scheduled Message ────────────

    [Fact]
    public async Task CreateScheduledMessage_ValidRequest_Returns201()
    {
        var ctx = await SetupAsync();
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken,
            new
            {
                content = "Hello from the future!",
                scheduledAt
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("conversationId").ReadLong().Should().Be(ctx.ConversationId);
        body.GetProperty("authorId").ReadLong().Should().Be(ctx.Owner.UserId);
        body.GetProperty("content").GetString().Should().Be("Hello from the future!");
        body.GetProperty("scheduledAt").GetDateTimeOffset().Should().BeCloseTo(scheduledAt, TimeSpan.FromSeconds(5));

        // SentAt should be null for a pending scheduled message
        body.GetProperty("sentAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task CreateScheduledMessage_EmptyContent_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken,
            new
            {
                content = "",
                scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10)
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateScheduledMessage_ContentTooLong_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken,
            new
            {
                content = new string('x', 4001),
                scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10)
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateScheduledMessage_ScheduledInPast_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken,
            new
            {
                content = "Too late!",
                scheduledAt = DateTimeOffset.UtcNow.AddSeconds(-10)
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateScheduledMessage_TooFarInFuture_Returns400()
    {
        var ctx = await SetupAsync();

        // More than 30 days in the future
        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken,
            new
            {
                content = "Way too far",
                scheduledAt = DateTimeOffset.UtcNow.AddDays(31)
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateScheduledMessage_NonExistentChannel_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/channels/999999999999/scheduled-messages",
            ctx.Owner.AccessToken,
            new
            {
                content = "Ghost channel",
                scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10)
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateScheduledMessage_AsNonMember_Returns403()
    {
        var ctx = await SetupAsync();
        var nonMember = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            nonMember.AccessToken,
            new
            {
                content = "Unauthorized schedule",
                scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateScheduledMessage_Unauthenticated_Returns401()
    {
        var ctx = await SetupAsync();

        var response = await _fixture.Client.PostJsonAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            new
            {
                content = "Unauthenticated",
                scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── List Scheduled Messages ────────────

    [Fact]
    public async Task ListScheduledMessages_ReturnsOnlyOwnPendingMessages()
    {
        var ctx = await SetupAsync();

        // Create two scheduled messages
        var scheduledAt1 = DateTimeOffset.UtcNow.AddMinutes(5);
        var scheduledAt2 = DateTimeOffset.UtcNow.AddMinutes(15);

        await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken,
            new { content = "First scheduled", scheduledAt = scheduledAt1 });

        await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken,
            new { content = "Second scheduled", scheduledAt = scheduledAt2 });

        // List them
        var response = await _helper.AuthGetAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var messages = body.EnumerateArray().ToList();
        messages.Should().HaveCountGreaterThanOrEqualTo(2);

        // Should be ordered by ScheduledAt ascending
        var firstContent = messages[0].GetProperty("content").GetString();
        var secondContent = messages[1].GetProperty("content").GetString();
        firstContent.Should().Be("First scheduled");
        secondContent.Should().Be("Second scheduled");
    }

    [Fact]
    public async Task ListScheduledMessages_OtherMember_DoesNotSeeOtherUsersMessages()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Owner creates a scheduled message
        await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken,
            new { content = "Owner's schedule", scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10) });

        // Member lists their scheduled messages (should be empty)
        var response = await _helper.AuthGetAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task ListScheduledMessages_NonExistentChannel_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/channels/999999999999/scheduled-messages",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListScheduledMessages_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.GetAsync(
            "/api/v1/channels/123456/scheduled-messages");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Delete Scheduled Message ────────────

    [Fact]
    public async Task DeleteScheduledMessage_AsAuthor_Returns204()
    {
        var ctx = await SetupAsync();

        // Create a scheduled message
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken,
            new { content = "Delete me", scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10) });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var scheduledMessageId = createBody.GetProperty("id").ReadLong();

        // Delete it
        var deleteResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages/{scheduledMessageId}",
            ctx.Owner.AccessToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it no longer appears in the list
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken);

        var listBody = await listResponse.ReadAsJsonAsync<JsonElement>();
        var remaining = listBody.EnumerateArray()
            .Where(m => m.GetProperty("id").ReadLong() == scheduledMessageId)
            .ToList();

        remaining.Should().BeEmpty("deleted scheduled message should not appear in listing");
    }

    [Fact]
    public async Task DeleteScheduledMessage_AsNonAuthor_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Owner creates a scheduled message
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages",
            ctx.Owner.AccessToken,
            new { content = "Owner's message", scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10) });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var scheduledMessageId = createBody.GetProperty("id").ReadLong();

        // Member tries to delete (should fail)
        var deleteResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages/{scheduledMessageId}",
            member.AccessToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteScheduledMessage_NonExistent_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{ctx.ChannelId}/scheduled-messages/999999999999",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteScheduledMessage_NonExistentChannel_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthDeleteAsync(
            "/api/v1/channels/999999999999/scheduled-messages/999999999999",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteScheduledMessage_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.DeleteAsync(
            "/api/v1/channels/123456/scheduled-messages/789");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
