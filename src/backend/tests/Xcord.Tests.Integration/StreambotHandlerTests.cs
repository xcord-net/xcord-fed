using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Integration tests for the streambot CRUD handlers under
/// <c>/api/v1/channels/{id}/streambots</c> and <c>/api/v1/streambots/{id}</c>.
/// Verifies stream-key encryption at rest, permission enforcement, soft-delete
/// semantics, and the single-default-per-channel invariant.
/// </summary>
[Collection("WebApp")]
public class StreambotHandlerTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public StreambotHandlerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helpers ────────────

    /// <summary>
    /// Creates a server and a stage channel through the public API.
    /// </summary>
    /// <remarks>
    /// This used to write the Streaming capability straight to the database,
    /// because no ChannelType mapped to it and the create endpoint had no way to
    /// ask for one. ChannelType.Stage closed that gap, so the test now sets up
    /// its fixture the same way a user would.
    /// </remarks>
    private async Task<(long serverId, long channelId)> CreateServerAndChannelAsync(
        AuthenticatedUser owner,
        ChannelCapability? capabilities = null)
    {
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(
            owner.AccessToken, serverId, name: null, type: (int)ChannelType.Stage);
        var channelId = channel.GetProperty("id").ReadLong();

        // Only the cases that need a channel *without* Streaming override this.
        if (capabilities is not null)
        {
            await using var db = _fixture.CreateDbContext();
            var dbChannel = await db.Channels.FirstAsync(c => c.Id == channelId);
            dbChannel.Capabilities = capabilities.Value;
            await db.SaveChangesAsync();
        }

        return (serverId, channelId);
    }

    private static object BuildCreateRequest(
        string name = "YouTube Bot",
        string platform = "YouTube",
        string rtmpUrl = "rtmp://a.rtmp.youtube.com/live2",
        string streamKey = "secret123",
        bool isDefault = false)
        => new { name, platform, rtmpUrl, streamKey, isDefault };

    // ──────────── Create ────────────

    [Fact]
    public async Task CreateStreambot_ValidRequest_Returns201AndEncryptsStreamKey()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId) = await CreateServerAndChannelAsync(owner);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/streambots",
            owner.AccessToken,
            BuildCreateRequest(streamKey: "secret123"));

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("hasStreamKey").GetBoolean().Should().BeTrue();
        body.TryGetProperty("streamKey", out _).Should().BeFalse(
            "the raw stream key must never be echoed back");
        body.TryGetProperty("encryptedStreamKey", out _).Should().BeFalse(
            "the encrypted stream key bytes must never leave the server");

        var streambotId = body.GetProperty("id").ReadLong();

        await using var db = _fixture.CreateDbContext();
        var streambot = await db.StreamBots.AsNoTracking()
            .FirstAsync(s => s.Id == streambotId);

        streambot.EncryptedStreamKey.Should().NotBeEmpty();
        streambot.EncryptedStreamKey.Should().NotEqual(
            Encoding.UTF8.GetBytes("secret123"),
            "the stream key must be stored encrypted, not as plain UTF-8 bytes");
    }

    [Fact]
    public async Task ListStreambots_NeverReturnsStreamKey()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId) = await CreateServerAndChannelAsync(owner);

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/streambots",
            owner.AccessToken,
            BuildCreateRequest(streamKey: "secret123"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/streambots",
            owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var rawBody = await listResponse.Content.ReadAsStringAsync();
        rawBody.Should().NotContain("secret123",
            "the raw stream key must never appear anywhere in the list response");

        var list = JsonSerializer.Deserialize<JsonElement>(rawBody);
        list.GetArrayLength().Should().Be(1);
        list[0].GetProperty("hasStreamKey").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CreateStreambot_NonRtmpUrl_Returns400()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId) = await CreateServerAndChannelAsync(owner);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/streambots",
            owner.AccessToken,
            BuildCreateRequest(rtmpUrl: "http://example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateStreambot_WithoutManageBroadcasts_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var (serverId, channelId) = await CreateServerAndChannelAsync(owner);

        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/streambots",
            member.AccessToken,
            BuildCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Update ────────────

    [Fact]
    public async Task UpdateStreambot_WithStreamKey_ReEncrypts()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId) = await CreateServerAndChannelAsync(owner);

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/streambots",
            owner.AccessToken,
            BuildCreateRequest(streamKey: "original-key"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var streambotId = (await createResponse.ReadAsJsonAsync<JsonElement>())
            .GetProperty("id").ReadLong();

        byte[] originalEncrypted;
        await using (var db = _fixture.CreateDbContext())
        {
            originalEncrypted = (await db.StreamBots.AsNoTracking()
                .FirstAsync(s => s.Id == streambotId)).EncryptedStreamKey;
        }

        var patchResponse = await _helper.AuthPatchAsync(
            $"/api/v1/streambots/{streambotId}",
            owner.AccessToken,
            new { streamKey = "new-rotated-key" });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db2 = _fixture.CreateDbContext();
        var updated = await db2.StreamBots.AsNoTracking()
            .FirstAsync(s => s.Id == streambotId);

        updated.EncryptedStreamKey.Should().NotBeEmpty();
        updated.EncryptedStreamKey.Should().NotEqual(originalEncrypted,
            "re-encrypting a new key must produce different ciphertext");
        updated.EncryptedStreamKey.Should().NotEqual(
            Encoding.UTF8.GetBytes("new-rotated-key"));
    }

    [Fact]
    public async Task UpdateStreambot_WithoutStreamKey_PreservesEncryptedKey()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId) = await CreateServerAndChannelAsync(owner);

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/streambots",
            owner.AccessToken,
            BuildCreateRequest(streamKey: "keep-me"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var streambotId = (await createResponse.ReadAsJsonAsync<JsonElement>())
            .GetProperty("id").ReadLong();

        byte[] originalEncrypted;
        await using (var db = _fixture.CreateDbContext())
        {
            originalEncrypted = (await db.StreamBots.AsNoTracking()
                .FirstAsync(s => s.Id == streambotId)).EncryptedStreamKey;
        }

        var patchResponse = await _helper.AuthPatchAsync(
            $"/api/v1/streambots/{streambotId}",
            owner.AccessToken,
            new { name = "renamed" });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db2 = _fixture.CreateDbContext();
        var updated = await db2.StreamBots.AsNoTracking()
            .FirstAsync(s => s.Id == streambotId);

        updated.Name.Should().Be("renamed");
        updated.EncryptedStreamKey.Should().Equal(originalEncrypted,
            "omitting streamKey from the PATCH must leave the stored key unchanged");
    }

    // ──────────── Delete ────────────

    [Fact]
    public async Task DeleteStreambot_SoftDeletesAndHidesFromList()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId) = await CreateServerAndChannelAsync(owner);

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/streambots",
            owner.AccessToken,
            BuildCreateRequest());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var streambotId = (await createResponse.ReadAsJsonAsync<JsonElement>())
            .GetProperty("id").ReadLong();

        var deleteResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/streambots/{streambotId}", owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = _fixture.CreateDbContext();
        // Bypass the global soft-delete query filter to inspect the tombstoned row.
        var row = await db.StreamBots
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(s => s.Id == streambotId);

        row.DeletedAt.Should().NotBeNull(
            "DELETE must soft-delete (set DeletedAt), never hard-delete the row");

        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/streambots",
            owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listResponse.ReadAsJsonAsync<JsonElement>();
        list.GetArrayLength().Should().Be(0,
            "soft-deleted streambots must not appear in the list response");
    }

    // ──────────── Single-default invariant ────────────

    [Fact]
    public async Task CreateStreambot_WithIsDefault_DemotesPreviousDefault()
    {
        var owner = await _helper.RegisterUserAsync();
        var (_, channelId) = await CreateServerAndChannelAsync(owner);

        var responseA = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/streambots",
            owner.AccessToken,
            BuildCreateRequest(name: "Bot A", isDefault: true));
        responseA.StatusCode.Should().Be(HttpStatusCode.Created);
        var botAId = (await responseA.ReadAsJsonAsync<JsonElement>())
            .GetProperty("id").ReadLong();

        var responseB = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/streambots",
            owner.AccessToken,
            BuildCreateRequest(name: "Bot B", isDefault: true));
        responseB.StatusCode.Should().Be(HttpStatusCode.Created);
        var botBId = (await responseB.ReadAsJsonAsync<JsonElement>())
            .GetProperty("id").ReadLong();

        await using var db = _fixture.CreateDbContext();
        var botA = await db.StreamBots.AsNoTracking().FirstAsync(s => s.Id == botAId);
        var botB = await db.StreamBots.AsNoTracking().FirstAsync(s => s.Id == botBId);

        botA.IsDefault.Should().BeFalse(
            "creating a new default bot must demote the previously-default bot");
        botB.IsDefault.Should().BeTrue();
    }
}
