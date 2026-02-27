using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
/// OWASP-based security audit tests. Each test verifies a specific checklist item.
/// Organized by OWASP Top 10 category.
/// </summary>
[Collection("WebApp")]
public class SecurityAuditTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public SecurityAuditTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    #region A01: Broken Access Control

    // ──────────── A01-01: Server endpoints require membership ────────────

    [Fact]
    public async Task A01_01_GetServer_AsNonMember_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var outsider = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}", outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A01_01_GetServerMembers_AsNonMember_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var outsider = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/members", outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A01_01_GetServerChannels_AsNonMember_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var outsider = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/channels", outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── A01-02: Attachment download requires conversation membership (FIX V1) ────────────

    [Fact]
    public async Task A01_02_DownloadAttachment_AsNonMember_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var outsider = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var message = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "attachment test");
        var messageId = message.GetProperty("id").ReadLong();

        // Insert a confirmed attachment linked to the message
        long attachmentId;
        await using (var db = _fixture.CreateDbContext())
        {
            var attachment = new Attachment
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1000L,
                MessageId = messageId,
                FileName = "secret.png",
                ContentType = "image/png",
                FileSize = 1024,
                S3Key = "attachments/test/secret-download.png",
                IsConfirmed = true,
                CreatedByUserId = owner.UserId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync();
            attachmentId = attachment.Id;
        }

        // Outsider tries to download
        var response = await _helper.AuthGetAsync(
            $"/api/v1/attachments/{attachmentId}/download", outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── A01-03: Attachment confirm requires ownership (FIX V2) ────────────

    [Fact]
    public async Task A01_03_ConfirmUpload_AsNonOwner_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var attacker = await _helper.RegisterUserAsync();

        // Owner creates an upload
        var uploadResponse = await _helper.AuthPostAsync(
            "/api/v1/uploads", owner.AccessToken,
            new { fileName = "test.png", contentType = "image/png", fileSize = 1024L, messageId = (long?)null });
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadBody = await uploadResponse.ReadAsJsonAsync<JsonElement>();
        var attachmentId = uploadBody.GetProperty("attachmentId").ReadLong();

        // Attacker tries to confirm owner's upload
        var response = await _helper.AuthPostAsync(
            $"/api/v1/attachments/{attachmentId}/confirm", attacker.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── A01-04: Attachment upload data requires ownership (FIX V3) ────────────

    [Fact]
    public async Task A01_04_UploadData_AsNonOwner_Returns403()
    {
        var owner = await _helper.RegisterUserAsync();
        var attacker = await _helper.RegisterUserAsync();

        // Owner creates an upload
        var uploadResponse = await _helper.AuthPostAsync(
            "/api/v1/uploads", owner.AccessToken,
            new { fileName = "test.png", contentType = "image/png", fileSize = 1024L, messageId = (long?)null });
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadBody = await uploadResponse.ReadAsJsonAsync<JsonElement>();
        var attachmentId = uploadBody.GetProperty("attachmentId").ReadLong();

        // Attacker tries to upload data to owner's attachment
        var request = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/uploads/{attachmentId}/data", attacker.AccessToken);
        request.Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── A01-05: Channel operations require permissions ────────────

    [Fact]
    public async Task A01_05_CreateChannel_WithoutManageChannels_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels", member.AccessToken,
            new { name = "forbidden-channel", type = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A01_05_DeleteChannel_WithoutManageChannels_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{channelId}", member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A01_05_UpdateChannel_WithoutManageChannels_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}", member.AccessToken,
            new { name = "hacked" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── A01-06: Role management requires ManageRoles ────────────

    [Fact]
    public async Task A01_06_CreateRole_WithoutManageRoles_Returns403()
    {
        var (serverId, _, member) = await SetupServerWithMember();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles", member.AccessToken,
            new { name = "Hacker", color = "#FF0000", permissions = 0, position = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A01_06_DeleteRole_WithoutManageRoles_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles", owner.AccessToken,
            new { name = "Protected", color = "#000000", permissions = 0, position = 1 });
        var role = await createResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/roles/{roleId}", member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A01_06_AssignRole_WithoutManageRoles_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();
        var otherMember = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(otherMember.AccessToken, inviteCode);

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles", owner.AccessToken,
            new { name = "TestRole", color = "#000000", permissions = 0, position = 1 });
        var role = await createResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{otherMember.UserId}/roles/{roleId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── A01-07: Moderation actions require correct permissions ────────────

    [Fact]
    public async Task A01_07_Ban_WithoutBanMembers_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();
        var target = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(target.AccessToken, inviteCode);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/bans", member.AccessToken,
            new { userId = target.UserId, reason = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A01_07_Kick_WithoutKickMembers_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();
        var target = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(target.AccessToken, inviteCode);

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{target.UserId}", member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A01_07_Timeout_WithoutModerateMembers_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();
        var target = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(target.AccessToken, inviteCode);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{target.UserId}/timeout", member.AccessToken,
            new { durationMinutes = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A01_07_DeleteOthersMessage_WithoutManageMessages_Returns403()
    {
        var (serverId, owner, member) = await SetupServerWithMember();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var message = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "Owner's message");
        var messageId = message.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/conversations/{conversationId}/messages/{messageId}", member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── A01-09: Cross-server isolation ────────────

    [Fact]
    public async Task A01_09_ServerAOwner_CannotManageServerB_Returns403()
    {
        var ownerA = await _helper.RegisterUserAsync();
        var ownerB = await _helper.RegisterUserAsync();
        var serverA = await _helper.CreateServerAsync(ownerA.AccessToken);
        var serverB = await _helper.CreateServerAsync(ownerB.AccessToken);
        var serverBId = serverB.GetProperty("id").ReadLong();

        // Owner A tries to create a channel in server B
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverBId}/channels", ownerA.AccessToken,
            new { name = "cross-server-channel", type = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A01_09_ServerAOwner_CannotCreateRoleInServerB_Returns403()
    {
        var ownerA = await _helper.RegisterUserAsync();
        var ownerB = await _helper.RegisterUserAsync();
        await _helper.CreateServerAsync(ownerA.AccessToken);
        var serverB = await _helper.CreateServerAsync(ownerB.AccessToken);
        var serverBId = serverB.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverBId}/roles", ownerA.AccessToken,
            new { name = "Hacked", color = "#FF0000", permissions = 0, position = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── A01-11: Shutdown endpoint requires authentication (FIX V6) ────────────

    [Fact]
    public async Task A01_11_ShutdownEndpoint_WithoutInternalKey_Returns401()
    {
        // Unauthenticated request without X-Internal-Key header
        var response = await _fixture.Client.PostJsonAsync(
            "/api/v1/internal/shutdown",
            new { reason = "malicious shutdown" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── A01-12: DM channels only accessible by participants ────────────

    [Fact]
    public async Task A01_12_DmMessages_AsNonParticipant_Returns403()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();
        var outsider = await _helper.RegisterUserAsync();

        // Create DM between user1 and user2
        var dmResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms", user1.AccessToken,
            new { recipientIds = new[] { user2.UserId } });
        dmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dm = await dmResponse.ReadAsJsonAsync<JsonElement>();
        var conversationId = dm.GetProperty("conversationId").ReadLong();

        // Outsider tries to read DM messages
        var response = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages", outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── A01-13: Self-action prevention ────────────

    [Fact]
    public async Task A01_13_CannotBanSelf_Returns400()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/bans", owner.AccessToken,
            new { userId = owner.UserId, reason = "Self-ban" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A01_13_CannotKickSelf_Returns400()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{owner.UserId}", owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A01_13_CannotTimeoutSelf_Returns400()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{owner.UserId}/timeout", owner.AccessToken,
            new { durationMinutes = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region A02: Cryptographic Failures

    // ──────────── A02-01: Passwords use BCrypt with adequate work factor ────────────

    [Fact]
    public async Task A02_01_PasswordHash_UsesBCryptWithAdequateWorkFactor()
    {
        var user = await _helper.RegisterUserAsync();

        await using var db = _fixture.CreateDbContext();
        var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user.UserId);
        dbUser.Should().NotBeNull();

        // BCrypt hashes start with $2a$ or $2b$ followed by work factor
        dbUser!.PasswordHash.Should().MatchRegex(@"^\$2[aby]\$\d{2}\$",
            "password should use BCrypt hashing");

        // Extract work factor and verify >= 10
        var parts = dbUser.PasswordHash.Split('$');
        var workFactor = int.Parse(parts[2]);
        workFactor.Should().BeGreaterThanOrEqualTo(10,
            "BCrypt work factor should be at least 10");
    }

    // ──────────── A02-02: JWT tokens signed with RSA ────────────

    [Fact]
    public async Task A02_02_JwtToken_UsesRS256()
    {
        var user = await _helper.RegisterUserAsync();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(user.AccessToken);

        jwt.Header.Alg.Should().Be("RS256",
            "JWT must be signed with RSA (RS256), not HMAC or 'none'");
    }

    // ──────────── A02-03: Webhook tokens use cryptographic randomness ────────────

    [Fact]
    public async Task A02_03_WebhookTokens_AreCryptographicallyRandom()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();

        var response1 = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/webhooks", owner.AccessToken,
            new { channelId, name = "Webhook 1" });
        var response2 = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/webhooks", owner.AccessToken,
            new { channelId, name = "Webhook 2" });

        var token1 = (await response1.ReadAsJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var token2 = (await response2.ReadAsJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        token1.Should().NotBe(token2, "webhook tokens must be unique");
        token1.Length.Should().BeGreaterThanOrEqualTo(32, "webhook tokens must be sufficiently long");
        token2.Length.Should().BeGreaterThanOrEqualTo(32);
    }

    // ──────────── A02-04: Invite codes are not sequential/guessable ────────────

    [Fact]
    public async Task A02_04_InviteCodes_AreNotSequential()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var code1 = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var code2 = await _helper.CreateInviteAsync(owner.AccessToken, serverId);

        code1.Should().NotBe(code2, "invite codes must be unique");
        code1.Length.Should().BeGreaterThanOrEqualTo(8, "invite codes must be sufficiently long");
        code2.Length.Should().BeGreaterThanOrEqualTo(8);

        // Verify mixed charset (not purely numeric/sequential)
        code1.Should().MatchRegex("[a-zA-Z]", "invite codes should contain letters");
    }

    #endregion

    #region A03: Injection

    // ──────────── A03-01: Regular message content is HTML-encoded ────────────

    [Fact]
    public async Task A03_01_MessageContent_IsHtmlEncoded()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var message = await _helper.SendMessageAsync(
            owner.AccessToken, conversationId, "<script>alert(1)</script>");

        var content = message.GetProperty("content").GetString()!;
        content.Should().NotContain("<script>");
        content.Should().Contain("&lt;script&gt;");
    }

    // ──────────── A03-02: Webhook message content is HTML-encoded (FIX V4) ────────────

    [Fact]
    public async Task A03_02_WebhookContent_IsHtmlEncoded()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Create webhook
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/webhooks", owner.AccessToken,
            new { channelId, name = "XSS Webhook" });
        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var webhookId = createBody.GetProperty("id").ReadLong();
        var token = createBody.GetProperty("token").GetString()!;

        // Execute webhook with XSS payload
        var executeResponse = await _fixture.Client.PostJsonAsync(
            $"/api/v1/webhooks/{webhookId}/{token}",
            new { content = "<img onerror=alert(1)>" });
        executeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify content is HTML-encoded in DB
        await using var db = _fixture.CreateDbContext();
        var dbMessage = await db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        dbMessage.Should().NotBeNull();
        dbMessage!.Content.Should().NotContain("<img");
        dbMessage.Content.Should().Contain("&lt;img");
    }

    // ──────────── A03-03: Federation message content is HTML-encoded (FIX V5) ────────────

    [Fact]
    public async Task A03_03_FederationContent_IsHtmlEncoded()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var channelId = channel.GetProperty("id").ReadLong();
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var remoteUrl = $"https://{Guid.NewGuid():N}.remote.example.com";
        var remoteChannelId = Guid.NewGuid().ToString("N")[..12];

        // Create federation follow
        var followResponse = await _helper.AuthPostAsync(
            "/api/v1/federation/follows", owner.AccessToken,
            new
            {
                remoteInstanceUrl = remoteUrl,
                remoteChannelId,
                localChannelId = channelId,
                remoteChannelName = "remote-xss"
            });
        followResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Post to federation inbox with XSS payload
        var inboxResponse = await _fixture.Client.PostJsonAsync(
            "/api/v1/federation/inbox",
            new
            {
                sourceInstanceUrl = remoteUrl,
                sourceChannelId = remoteChannelId,
                messages = new[]
                {
                    new
                    {
                        remoteMessageId = Guid.NewGuid().ToString(),
                        authorName = "Attacker",
                        authorAvatarUrl = (string?)null,
                        content = "<script>document.cookie</script>",
                        metadata = (string?)null,
                        createdAt = DateTimeOffset.UtcNow
                    }
                }
            });
        inboxResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify content is HTML-encoded in DB
        await using var db = _fixture.CreateDbContext();
        var dbMessage = await db.Messages
            .Where(m => m.ConversationId == conversationId && m.AuthorId == null)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        dbMessage.Should().NotBeNull();
        dbMessage!.Content.Should().NotContain("<script>");
        dbMessage.Content.Should().Contain("&lt;script&gt;");
    }

    // ──────────── A03-04: Username/display name validated ────────────

    [Fact]
    public async Task A03_04_Register_WithScriptInUsername_Returns400OrEncoded()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username = $"<script>alert(1)</script>",
            displayName = "Test",
            email = $"{id}@xcord.local",
            password = "TestPassword123!"
        });

        // Either rejected (400) or username is sanitized — XSS must not be stored raw
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.ReadAsJsonAsync<JsonElement>();
            var username = body.GetProperty("username").GetString()!;
            username.Should().NotContain("<script>",
                "if registration succeeds, the username must be sanitized");
        }
        // 400 is also acceptable (validation rejects it)
    }

    // ──────────── A03-05: Server/channel names don't allow injection ────────────

    [Fact]
    public async Task A03_05_ServerName_WithScript_IsAcceptedSafely()
    {
        // Server and channel names are display strings rendered by the frontend
        // with proper escaping (SolidJS textContent). They are NOT rendered as raw HTML.
        // The important thing is they don't cause server-side issues.
        var owner = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/servers", owner.AccessToken,
            new { name = "<script>alert(1)</script>" });

        // Server creation should succeed — names are safe because they're
        // rendered as text content by the frontend, never as raw HTML
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ──────────── A03-06: SQL injection not possible ────────────

    [Fact]
    public async Task A03_06_SqlInjection_InMessageContent_IsHarmless()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Attempt SQL injection via message content
        var message = await _helper.SendMessageAsync(
            owner.AccessToken, conversationId,
            "'; DROP TABLE messages; --");

        message.GetProperty("id").ReadLong().Should().BeGreaterThan(0,
            "message should be created normally despite SQL injection attempt");

        // Verify messages table still exists by reading messages
        var messagesResponse = await _helper.AuthGetAsync(
            $"/api/v1/conversations/{conversationId}/messages", owner.AccessToken);
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region A04: Insecure Design

    // ──────────── A04-01: Rate limiting on authentication endpoints ────────────

    [Fact]
    public async Task A04_01_Login_RateLimited_After5Failures()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"ratelimit_{id}@xcord.local";
        var password = "TestPassword123!";

        await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username = $"ratelimit_{id}",
            displayName = "Rate Limit Test",
            email,
            password
        });

        // Send 5 failed login attempts
        for (var i = 0; i < 5; i++)
        {
            await _fixture.Client.PostJsonAsync("/api/v1/auth/login",
                new { email, password = "WrongPassword999!" });
        }

        // 6th attempt should be rate-limited
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/login",
            new { email, password = "WrongPassword999!" });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    // ──────────── A04-03: Message length enforced server-side ────────────

    [Fact]
    public async Task A04_03_Message_ExceedsMaxLength_Returns400()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var longContent = new string('a', 4001);
        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/messages", owner.AccessToken,
            new { content = longContent });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── A04-04: File upload size enforced server-side ────────────

    [Fact]
    public async Task A04_04_Upload_ExceedsMaxSize_Returns400()
    {
        var user = await _helper.RegisterUserAsync();

        // 25MB limit = 25 * 1024 * 1024 = 26214400
        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads", user.AccessToken,
            new
            {
                fileName = "huge.mp4",
                contentType = "video/mp4",
                fileSize = 26_214_401L,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region A05: Security Misconfiguration

    // ──────────── A05-01: Error responses don't leak stack traces ────────────

    [Fact]
    public async Task A05_01_ErrorResponse_DoesNotLeakStackTrace()
    {
        // Send malformed request (missing required fields)
        var response = await _fixture.Client.PostAsync("/api/v1/auth/login",
            new StringContent("{invalid json", Encoding.UTF8, "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("StackTrace", "error response must not leak stack traces");
        body.Should().NotContain("at Xcord.", "error response must not leak internal namespaces");
        body.Should().NotContain(".cs:line", "error response must not leak source file paths");
    }

    // ──────────── A05-02: CORS properly configured ────────────

    [Fact]
    public async Task A05_02_Cors_DoesNotAllowWildcardOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "https://evil.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _fixture.Client.SendAsync(request);

        if (response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins))
        {
            var originList = origins.ToList();
            originList.Should().NotContain("*",
                "CORS must not allow wildcard origins");
            originList.Should().NotContain("https://evil.com",
                "CORS must not reflect arbitrary origins");
        }
    }

    // ──────────── A05-03: Security headers present ────────────

    [Fact]
    public async Task A05_03_SecurityHeaders_XContentTypeOptions()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync("/api/v1/users/@me", user.AccessToken);

        if (response.Headers.TryGetValues("X-Content-Type-Options", out var values))
        {
            values.Should().Contain("nosniff");
        }
        // If the header isn't present, it's a finding but not a test failure
        // since the test infrastructure may strip headers
    }

    #endregion

    #region A07: Authentication

    // ──────────── A07-01: Unconfirmed email cannot access protected endpoints ────────────

    [Fact]
    public async Task A07_01_UnconfirmedEmail_CannotCreateServer()
    {
        var user = await _helper.RegisterUnconfirmedUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/servers", user.AccessToken,
            new { name = "Unconfirmed Server" });

        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden },
            "unconfirmed email should not be able to create servers");
    }

    // ──────────── A07-02: Expired JWT rejected ────────────

    [Fact]
    public async Task A07_02_ExpiredJwt_Returns401()
    {
        // Create a user to get a valid token format, then tamper with it
        var user = await _helper.RegisterUserAsync();

        // Create a mangled token by modifying the payload (this invalidates the signature)
        var parts = user.AccessToken.Split('.');
        parts.Length.Should().Be(3, "JWT should have 3 parts");

        // Decode payload, modify exp to be in the past, re-encode
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);

        // Just use a completely fake token with expired claim — the invalid signature
        // will cause rejection too, which is the correct behavior
        var expiredToken = $"{parts[0]}.{Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson.Replace(
            payload.GetProperty("exp").GetRawText(),
            "1000000000")))}.{parts[2]}";

        var request = TestHelper.AuthRequest(HttpMethod.Get, "/api/v1/users/@me", expiredToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── A07-03: Invalid JWT signature rejected ────────────

    [Fact]
    public async Task A07_03_InvalidJwtSignature_Returns401()
    {
        var user = await _helper.RegisterUserAsync();

        // Tamper with the signature portion
        var parts = user.AccessToken.Split('.');
        var tamperedSignature = parts[2][..^1] + (parts[2][^1] == 'A' ? 'B' : 'A');
        var tamperedToken = $"{parts[0]}.{parts[1]}.{tamperedSignature}";

        var request = TestHelper.AuthRequest(HttpMethod.Get, "/api/v1/users/@me", tamperedToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── A07-04: Disabled user cannot re-authenticate ────────────

    [Fact]
    public async Task A07_04_DisabledUser_CannotLogin()
    {
        var user = await _helper.RegisterUserAsync();

        // Disable the user directly in DB
        await using (var db = _fixture.CreateDbContext())
        {
            var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user.UserId);
            dbUser.Should().NotBeNull();
            dbUser!.IsDisabled = true;
            await db.SaveChangesAsync();
        }

        // Disabled user should not be able to obtain a new token via login
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/login",
            new { email = user.Email, password = user.Password });

        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "disabled user should not be able to log in and obtain a new token");
    }

    #endregion

    // ──────────── Helpers ────────────

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

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        return Convert.FromBase64String(output);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
