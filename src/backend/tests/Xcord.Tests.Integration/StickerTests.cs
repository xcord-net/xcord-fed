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
public class StickerTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public StickerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helper Methods ────────────

    private async Task<StickerTestContext> SetupAsync()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        return new StickerTestContext(owner, serverId);
    }

    private sealed record StickerTestContext(
        AuthenticatedUser Owner,
        long ServerId);

    /// <summary>
    /// Seeds a confirmed attachment directly in the database.
    /// Sticker creation requires a confirmed attachment -- since the integration test
    /// environment has no real S3, we seed the record directly.
    /// </summary>
    private async Task<long> SeedConfirmedAttachmentAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var attachmentId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        db.Attachments.Add(new Attachment
        {
            Id = attachmentId,
            FileName = "sticker.png",
            ContentType = "image/png",
            FileSize = 2048,
            S3Key = $"attachments/test/{attachmentId}/sticker.png",
            IsConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return attachmentId;
    }

    /// <summary>
    /// Creates a sticker pack via the API. Returns the response body.
    /// </summary>
    private async Task<JsonElement> CreateStickerPackAsync(
        string accessToken, long serverId,
        string? name = null, string? description = null)
    {
        name ??= $"Pack_{Guid.NewGuid():N}"[..20];

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/sticker-packs",
            accessToken,
            new
            {
                name,
                description
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"sticker pack creation should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Creates a sticker within a pack via the API. Returns the response body.
    /// </summary>
    private async Task<JsonElement> CreateStickerAsync(
        string accessToken, long serverId, long packId,
        string? name = null, string? tags = null)
    {
        var attachmentId = await SeedConfirmedAttachmentAsync();
        name ??= $"sticker_{Guid.NewGuid():N}"[..20];

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/sticker-packs/{packId}/stickers",
            accessToken,
            new
            {
                name,
                tags,
                attachmentId
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"sticker creation should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    // ──────────── Create Sticker Pack ────────────

    [Fact]
    public async Task CreateStickerPack_AsOwner_ReturnsPackDetails()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs",
            ctx.Owner.AccessToken,
            new
            {
                name = "Fun Stickers",
                description = "A pack of fun stickers"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);
        body.GetProperty("name").GetString().Should().Be("Fun Stickers");
        body.GetProperty("description").GetString().Should().Be("A pack of fun stickers");
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateStickerPack_WithoutDescription_ReturnsNullDescription()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs",
            ctx.Owner.AccessToken,
            new { name = "No Desc Pack" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("No Desc Pack");
        body.GetProperty("description").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task CreateStickerPack_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Member attempts to create a sticker pack (should fail -- no ManageStickers permission)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs",
            member.AccessToken,
            new { name = "Forbidden Pack" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateStickerPack_Unauthenticated_Returns401()
    {
        var ctx = await SetupAsync();

        var response = await _fixture.Client.PostJsonAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs",
            new { name = "Unauthenticated Pack" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateStickerPack_EmptyName_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs",
            ctx.Owner.AccessToken,
            new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Create Sticker ────────────

    [Fact]
    public async Task CreateSticker_AsOwner_ReturnsStickerDetails()
    {
        var ctx = await SetupAsync();
        var pack = await CreateStickerPackAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Test Pack");
        var packId = pack.GetProperty("id").ReadLong();
        var attachmentId = await SeedConfirmedAttachmentAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs/{packId}/stickers",
            ctx.Owner.AccessToken,
            new
            {
                name = "thumbs_up",
                tags = "thumbs,up,yes,approve",
                attachmentId
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("stickerPackId").ReadLong().Should().Be(packId);
        body.GetProperty("name").GetString().Should().Be("thumbs_up");
        body.GetProperty("tags").GetString().Should().Be("thumbs,up,yes,approve");
        body.GetProperty("imageUrl").GetString().Should().Contain("/api/v1/attachments/");
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateSticker_WithoutTags_ReturnsNullTags()
    {
        var ctx = await SetupAsync();
        var pack = await CreateStickerPackAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var packId = pack.GetProperty("id").ReadLong();
        var attachmentId = await SeedConfirmedAttachmentAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs/{packId}/stickers",
            ctx.Owner.AccessToken,
            new
            {
                name = "no_tags_sticker",
                attachmentId
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("no_tags_sticker");
        body.GetProperty("tags").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task CreateSticker_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var pack = await CreateStickerPackAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var packId = pack.GetProperty("id").ReadLong();
        var attachmentId = await SeedConfirmedAttachmentAsync();

        // Member attempts to create a sticker (should fail -- no ManageStickers permission)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs/{packId}/stickers",
            member.AccessToken,
            new
            {
                name = "forbidden_sticker",
                attachmentId
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateSticker_NonExistentPack_Returns404()
    {
        var ctx = await SetupAsync();
        var attachmentId = await SeedConfirmedAttachmentAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs/999999999999/stickers",
            ctx.Owner.AccessToken,
            new
            {
                name = "orphan_sticker",
                attachmentId
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateSticker_NonExistentAttachment_Returns404()
    {
        var ctx = await SetupAsync();
        var pack = await CreateStickerPackAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var packId = pack.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs/{packId}/stickers",
            ctx.Owner.AccessToken,
            new
            {
                name = "ghost_sticker",
                attachmentId = 999999999999L
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateSticker_UnconfirmedAttachment_Returns400()
    {
        var ctx = await SetupAsync();
        var pack = await CreateStickerPackAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var packId = pack.GetProperty("id").ReadLong();

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
                FileSize = 2048,
                S3Key = $"attachments/test/{attachmentId}/unconfirmed.png",
                IsConfirmed = false,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs/{packId}/stickers",
            ctx.Owner.AccessToken,
            new
            {
                name = "unconfirmed_sticker",
                attachmentId
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSticker_EmptyName_Returns400()
    {
        var ctx = await SetupAsync();
        var pack = await CreateStickerPackAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var packId = pack.GetProperty("id").ReadLong();
        var attachmentId = await SeedConfirmedAttachmentAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs/{packId}/stickers",
            ctx.Owner.AccessToken,
            new
            {
                name = "",
                attachmentId
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── List Sticker Packs ────────────

    [Fact]
    public async Task ListStickerPacks_AsOwner_ReturnsPacksWithStickers()
    {
        var ctx = await SetupAsync();

        // Create a pack with stickers
        var pack = await CreateStickerPackAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Listed Pack", description: "A listed pack");
        var packId = pack.GetProperty("id").ReadLong();

        await CreateStickerAsync(ctx.Owner.AccessToken, ctx.ServerId, packId, name: "sticker_alpha");
        await CreateStickerAsync(ctx.Owner.AccessToken, ctx.ServerId, packId, name: "sticker_beta");

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var packs = body.GetProperty("stickerPacks").EnumerateArray().ToList();
        packs.Should().HaveCountGreaterThanOrEqualTo(1);

        var listedPack = packs.First(p => p.GetProperty("id").ReadLong() == packId);
        listedPack.GetProperty("name").GetString().Should().Be("Listed Pack");
        listedPack.GetProperty("description").GetString().Should().Be("A listed pack");
        listedPack.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);

        var stickers = listedPack.GetProperty("stickers").EnumerateArray().ToList();
        stickers.Should().HaveCount(2);

        var stickerNames = stickers.Select(s => s.GetProperty("name").GetString()).ToList();
        stickerNames.Should().Contain("sticker_alpha");
        stickerNames.Should().Contain("sticker_beta");

        // Verify sticker fields
        foreach (var sticker in stickers)
        {
            sticker.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
            sticker.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
            sticker.GetProperty("imageUrl").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task ListStickerPacks_AsMember_ReturnsPacks()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        await CreateStickerPackAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Member Visible Pack");

        // Member can list sticker packs (read-only -- no permission required beyond membership)
        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var packs = body.GetProperty("stickerPacks").EnumerateArray().ToList();
        var names = packs.Select(p => p.GetProperty("name").GetString()).ToList();
        names.Should().Contain("Member Visible Pack");
    }

    [Fact]
    public async Task ListStickerPacks_AsNonMember_Returns403()
    {
        var ctx = await SetupAsync();
        var outsider = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListStickerPacks_EmptyServer_ReturnsEmptyList()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var packs = body.GetProperty("stickerPacks").EnumerateArray().ToList();
        packs.Should().BeEmpty();
    }

    // ──────────── Delete Sticker ────────────

    [Fact]
    public async Task DeleteSticker_AsOwner_Returns204()
    {
        var ctx = await SetupAsync();
        var pack = await CreateStickerPackAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Delete Test Pack");
        var packId = pack.GetProperty("id").ReadLong();

        var created = await CreateStickerAsync(ctx.Owner.AccessToken, ctx.ServerId, packId, name: "delete_me");
        var stickerId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/stickers/{stickerId}",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it no longer appears in pack listing
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/sticker-packs",
            ctx.Owner.AccessToken);

        var listBody = await listResponse.ReadAsJsonAsync<JsonElement>();
        var packs = listBody.GetProperty("stickerPacks").EnumerateArray().ToList();
        var targetPack = packs.FirstOrDefault(p => p.GetProperty("id").ReadLong() == packId);

        if (targetPack.ValueKind != JsonValueKind.Undefined)
        {
            var stickers = targetPack.GetProperty("stickers").EnumerateArray()
                .Where(s => s.GetProperty("id").ReadLong() == stickerId)
                .ToList();

            stickers.Should().BeEmpty("deleted sticker should not appear in pack listing");
        }
    }

    [Fact]
    public async Task DeleteSticker_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var pack = await CreateStickerPackAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var packId = pack.GetProperty("id").ReadLong();

        var created = await CreateStickerAsync(ctx.Owner.AccessToken, ctx.ServerId, packId, name: "protected_sticker");
        var stickerId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/stickers/{stickerId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteSticker_NonExistent_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/stickers/999999999999",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
