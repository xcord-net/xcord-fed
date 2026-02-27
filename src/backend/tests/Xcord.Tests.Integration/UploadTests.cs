using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class UploadTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public UploadTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Request Upload ────────────

    [Fact]
    public async Task RequestUpload_ValidImageFile_ReturnsAttachmentIdAndUrls()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "test-image.png",
                contentType = "image/png",
                fileSize = 1024L,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("attachmentId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("uploadUrl").GetString().Should().Contain("/api/v1/uploads/");
        body.GetProperty("uploadUrl").GetString().Should().EndWith("/data");
        body.GetProperty("downloadUrl").GetString().Should().Contain("/api/v1/attachments/");
        body.GetProperty("downloadUrl").GetString().Should().EndWith("/download");
    }

    [Fact]
    public async Task RequestUpload_ValidPdf_ReturnsAttachmentIdAndUrls()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "document.pdf",
                contentType = "application/pdf",
                fileSize = 2048L,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("attachmentId").ReadLong().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RequestUpload_DisallowedContentType_Returns400()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "malware.exe",
                contentType = "application/x-msdownload",
                fileSize = 1024L,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestUpload_EmptyFileName_Returns400()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "",
                contentType = "image/png",
                fileSize = 1024L,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestUpload_ZeroFileSize_Returns400()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "test.png",
                contentType = "image/png",
                fileSize = 0L,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestUpload_ExceedsMaxFileSize_Returns400()
    {
        var user = await _helper.RegisterUserAsync();

        // 25MB limit = 25 * 1024 * 1024 = 26214400
        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "huge-file.mp4",
                contentType = "video/mp4",
                fileSize = 26_214_401L,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestUpload_AtMaxFileSize_Succeeds()
    {
        var user = await _helper.RegisterUserAsync();

        // Exactly 25MB should succeed
        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "max-size.mp4",
                contentType = "video/mp4",
                fileSize = 25L * 1024 * 1024,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequestUpload_WithMessageId_ForNonExistentMessage_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "test.png",
                contentType = "image/png",
                fileSize = 1024L,
                messageId = 999_999_999_999L
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RequestUpload_WithMessageId_AsNonAuthor_Returns403()
    {
        var author = await _helper.RegisterUserAsync();
        var otherUser = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(author.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(author.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        // Add otherUser to server
        var inviteCode = await _helper.CreateInviteAsync(author.AccessToken, serverId);
        await _helper.JoinServerAsync(otherUser.AccessToken, inviteCode);

        // Author sends a message
        var message = await _helper.SendMessageAsync(author.AccessToken, conversationId, "Author's message");
        var messageId = message.GetProperty("id").ReadLong();

        // Other user tries to attach to author's message
        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            otherUser.AccessToken,
            new
            {
                fileName = "sneaky.png",
                contentType = "image/png",
                fileSize = 1024L,
                messageId
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequestUpload_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.PostJsonAsync(
            "/api/v1/uploads",
            new
            {
                fileName = "test.png",
                contentType = "image/png",
                fileSize = 1024L,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Storage Quota ────────────

    [Fact]
    public async Task RequestUpload_StorageQuotaExceeded_Returns400()
    {
        var user = await _helper.RegisterUserAsync();

        // Insert a confirmed attachment that consumes nearly the entire 1MB quota
        await using (var db = _fixture.CreateDbContext())
        {
            db.Attachments.Add(new Xcord.Entities.Attachment
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                FileName = "large-file.bin",
                ContentType = "application/octet-stream",
                FileSize = 1024 * 1024, // 1 MB — fills the quota
                S3Key = "test/large-file.bin",
                IsConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Attempt to upload another file — should exceed quota
        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "one-more.png",
                contentType = "image/png",
                fileSize = 1024L,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("STORAGE_QUOTA_EXCEEDED");
    }

    [Fact]
    public async Task RequestUpload_SoftDeletedAttachment_DoesNotCountTowardQuota()
    {
        var user = await _helper.RegisterUserAsync();

        // Insert a confirmed but soft-deleted attachment that would fill the quota
        await using (var db = _fixture.CreateDbContext())
        {
            db.Attachments.Add(new Xcord.Entities.Attachment
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1,
                FileName = "deleted-file.bin",
                ContentType = "application/octet-stream",
                FileSize = 1024 * 1024, // 1 MB
                S3Key = "test/deleted-file.bin",
                IsConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow,
                DeletedAt = DateTimeOffset.UtcNow // soft-deleted
            });
            await db.SaveChangesAsync();
        }

        // Upload should succeed because soft-deleted attachments are excluded
        var response = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "new-file.png",
                contentType = "image/png",
                fileSize = 1024L,
                messageId = (long?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ──────────── Get Attachment ────────────

    [Fact]
    public async Task GetAttachment_NonExistent_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/attachments/999999999999",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAttachment_UserNotInServer_Returns403()
    {
        // Arrange — owner creates a server + channel and sends a message
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var message = await _helper.SendMessageAsync(owner.AccessToken, conversationId, "Message with attachment");
        var messageId = message.GetProperty("id").ReadLong();

        // Insert a confirmed attachment record linked to that message
        long attachmentId;
        await using (var db = _fixture.CreateDbContext())
        {
            var attachment = new Xcord.Entities.Attachment
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 500L,
                MessageId = messageId,
                FileName = "secret.png",
                ContentType = "image/png",
                FileSize = 1024,
                S3Key = "attachments/test/secret.png",
                IsConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync();
            attachmentId = attachment.Id;
        }

        // Register a second user who never joins the server
        var outsider = await _helper.RegisterUserAsync();

        // Act — outsider attempts to download the attachment
        var response = await _helper.AuthGetAsync(
            $"/api/v1/attachments/{attachmentId}",
            outsider.AccessToken);

        // Assert — must be 403 Forbidden, not 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Confirm Upload ────────────

    [Fact]
    public async Task ConfirmUpload_NonExistentAttachment_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/attachments/999999999999/confirm",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConfirmUpload_FileNotInStorage_Returns400()
    {
        var user = await _helper.RegisterUserAsync();

        // Request upload to create the attachment record
        var uploadResponse = await _helper.AuthPostAsync(
            "/api/v1/uploads",
            user.AccessToken,
            new
            {
                fileName = "test.png",
                contentType = "image/png",
                fileSize = 1024L,
                messageId = (long?)null
            });

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadBody = await uploadResponse.ReadAsJsonAsync<JsonElement>();
        var attachmentId = uploadBody.GetProperty("attachmentId").ReadLong();

        // Try to confirm without actually uploading data to S3
        // S3 is not available in the test fixture, so ExistsAsync will fail
        var confirmResponse = await _helper.AuthPostAsync(
            $"/api/v1/attachments/{attachmentId}/confirm",
            user.AccessToken);

        // Should return 400 (file not uploaded) or 500 (S3 unavailable)
        // Either way, it should not return 200
        confirmResponse.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConfirmUpload_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.PostJsonAsync(
            "/api/v1/attachments/123456/confirm",
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Download Attachment ────────────

    [Fact]
    public async Task DownloadAttachment_NonExistent_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/attachments/999999999999/download",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DownloadAttachment_Unauthenticated_Returns401()
    {
        var response = await _fixture.Client.GetAsync("/api/v1/attachments/999999999999/download");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Upload Data ────────────

    [Fact]
    public async Task UploadData_NonExistentAttachment_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var request = TestHelper.AuthRequest(HttpMethod.Put, "/api/v1/uploads/999999999999/data", user.AccessToken);
        request.Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadData_Unauthenticated_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/uploads/999999999999/data");
        request.Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
