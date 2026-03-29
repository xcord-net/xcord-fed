using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class EmojiTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public EmojiTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helper Methods ────────────

    private async Task<EmojiTestContext> SetupAsync()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        return new EmojiTestContext(owner, serverId);
    }

    private sealed record EmojiTestContext(
        AuthenticatedUser Owner,
        long ServerId);

    /// <summary>
    /// Seeds a confirmed attachment directly in the database.
    /// Emoji creation requires a confirmed attachment - since the integration test
    /// environment has no real S3, we seed the record directly.
    /// </summary>
    private async Task<long> SeedConfirmedAttachmentAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var attachmentId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        db.Attachments.Add(new Attachment
        {
            Id = attachmentId,
            FileName = "emoji.png",
            ContentType = "image/png",
            FileSize = 1024,
            S3Key = $"attachments/test/{attachmentId}/emoji.png",
            IsConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return attachmentId;
    }

    /// <summary>
    /// Creates an emoji via the API. Returns the response body.
    /// </summary>
    private async Task<JsonElement> CreateEmojiAsync(
        string accessToken, long serverId,
        string? name = null, bool isAnimated = false)
    {
        var attachmentId = await SeedConfirmedAttachmentAsync();
        name ??= $"emoji_{Guid.NewGuid():N}"[..20];

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/emojis",
            accessToken,
            new
            {
                name,
                attachmentId,
                isAnimated
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"emoji creation should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    // ──────────── Create Emoji ────────────

    [Fact]
    public async Task CreateEmoji_AsOwner_ReturnsEmojiDetails()
    {
        var ctx = await SetupAsync();
        var attachmentId = await SeedConfirmedAttachmentAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            ctx.Owner.AccessToken,
            new
            {
                name = "pepe_happy",
                attachmentId,
                isAnimated = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);
        body.GetProperty("name").GetString().Should().Be("pepe_happy");
        body.GetProperty("imageUrl").GetString().Should().Contain("/api/v1/attachments/");
        body.GetProperty("isAnimated").GetBoolean().Should().BeFalse();
        body.GetProperty("creatorId").ReadLong().Should().Be(ctx.Owner.UserId);
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateEmoji_Animated_ReturnsAnimatedTrue()
    {
        var ctx = await SetupAsync();
        var attachmentId = await SeedConfirmedAttachmentAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            ctx.Owner.AccessToken,
            new
            {
                name = "dancing_parrot",
                attachmentId,
                isAnimated = true
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("isAnimated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CreateEmoji_DuplicateName_Returns409()
    {
        var ctx = await SetupAsync();

        // Create first emoji
        await CreateEmojiAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "unique_emoji");

        // Try to create another with the same name
        var attachmentId = await SeedConfirmedAttachmentAsync();
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            ctx.Owner.AccessToken,
            new
            {
                name = "unique_emoji",
                attachmentId,
                isAnimated = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateEmoji_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var attachmentId = await SeedConfirmedAttachmentAsync();

        // Member attempts to create an emoji (should fail -- no ManageEmojis permission)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            member.AccessToken,
            new
            {
                name = "forbidden_emoji",
                attachmentId,
                isAnimated = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateEmoji_Unauthenticated_Returns401()
    {
        var ctx = await SetupAsync();
        var attachmentId = await SeedConfirmedAttachmentAsync();

        var response = await _fixture.Client.PostJsonAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            new
            {
                name = "unauthenticated_emoji",
                attachmentId,
                isAnimated = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEmoji_NonExistentAttachment_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            ctx.Owner.AccessToken,
            new
            {
                name = "ghost_emoji",
                attachmentId = 999999999999L,
                isAnimated = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateEmoji_UnconfirmedAttachment_Returns400()
    {
        var ctx = await SetupAsync();

        // Seed an unconfirmed attachment
        long attachmentId;
        await using (var db = _fixture.CreateDbContext())
        {
            attachmentId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1;
            db.Attachments.Add(new Attachment
            {
                Id = attachmentId,
                FileName = "unconfirmed.png",
                ContentType = "image/png",
                FileSize = 1024,
                S3Key = $"attachments/test/{attachmentId}/unconfirmed.png",
                IsConfirmed = false,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            ctx.Owner.AccessToken,
            new
            {
                name = "unconfirmed_emoji",
                attachmentId,
                isAnimated = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── List Emojis ────────────

    [Fact]
    public async Task ListEmojis_AsOwner_ReturnsCreatedEmojis()
    {
        var ctx = await SetupAsync();

        // Create two emojis
        await CreateEmojiAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "emoji_one");
        await CreateEmojiAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "emoji_two");

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var emojis = body.GetProperty("emojis").EnumerateArray().ToList();
        emojis.Should().HaveCountGreaterThanOrEqualTo(2);

        var names = emojis.Select(e => e.GetProperty("name").GetString()).ToList();
        names.Should().Contain("emoji_one");
        names.Should().Contain("emoji_two");

        // Verify all emojis have expected fields
        foreach (var emoji in emojis)
        {
            emoji.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
            emoji.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);
            emoji.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
            emoji.GetProperty("imageUrl").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task ListEmojis_AsMember_ReturnsEmojis()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        await CreateEmojiAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "member_visible");

        // Member can list emojis (read-only -- no permission required beyond membership)
        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var emojis = body.GetProperty("emojis").EnumerateArray().ToList();
        var names = emojis.Select(e => e.GetProperty("name").GetString()).ToList();
        names.Should().Contain("member_visible");
    }

    [Fact]
    public async Task ListEmojis_AsNonMember_Returns403()
    {
        var ctx = await SetupAsync();
        var outsider = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListEmojis_EmptyServer_ReturnsEmptyList()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var emojis = body.GetProperty("emojis").EnumerateArray().ToList();
        emojis.Should().BeEmpty();
    }

    // ──────────── Delete Emoji ────────────

    [Fact]
    public async Task DeleteEmoji_AsOwner_Returns204()
    {
        var ctx = await SetupAsync();
        var created = await CreateEmojiAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "delete_me");
        var emojiId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis/{emojiId}",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it no longer appears in the list
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis",
            ctx.Owner.AccessToken);

        var listBody = await listResponse.ReadAsJsonAsync<JsonElement>();
        var emojis = listBody.GetProperty("emojis").EnumerateArray()
            .Where(e => e.GetProperty("id").ReadLong() == emojiId)
            .ToList();

        emojis.Should().BeEmpty("deleted emoji should not appear in listing");
    }

    [Fact]
    public async Task DeleteEmoji_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var created = await CreateEmojiAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "protected_emoji");
        var emojiId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis/{emojiId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteEmoji_NonExistent_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/emojis/999999999999",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
