using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class ForumTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public ForumTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helper Methods ────────────

    /// <summary>
    /// Creates a server with a forum channel (type=3). Returns serverId, channelId, and access token.
    /// </summary>
    private async Task<(long ServerId, long ChannelId, string AccessToken)> SetupServerWithForumChannel()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId, "test-forum", type: 3);
        var channelId = channel.GetProperty("id").ReadLong();
        return (serverId, channelId, user.AccessToken);
    }

    /// <summary>
    /// Creates a forum post in the given channel. Returns the response body.
    /// </summary>
    private async Task<JsonElement> CreateForumPostAsync(
        string accessToken, long channelId, string title = "Test Post", string content = "Test content", List<string>? tags = null)
    {
        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/posts",
            accessToken,
            new { title, content, tags = tags ?? new List<string>() });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"forum post creation should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Creates a forum tag in the given channel. Returns the response body.
    /// </summary>
    private async Task<JsonElement> CreateForumTagAsync(
        string accessToken, long channelId, string name = "test-tag")
    {
        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/tags",
            accessToken,
            new { name });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"forum tag creation should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Sets up a server with a forum channel and a second user who has joined as a member (non-owner).
    /// </summary>
    private async Task<(long ServerId, long ChannelId, AuthenticatedUser Owner, AuthenticatedUser Member)> SetupServerWithForumAndMember()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();

        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId, "test-forum", type: 3);
        var channelId = channel.GetProperty("id").ReadLong();

        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return (serverId, channelId, owner, member);
    }

    // ──────────── Create Forum Channel ────────────

    [Fact]
    public async Task CreateForumChannel_AsOwner_Returns201WithForumType()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            owner.AccessToken,
            new { name = "forum-channel", type = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("conversationId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("serverId").ReadLong().Should().Be(serverId);
        body.GetProperty("name").GetString().Should().Be("forum-channel");
        body.GetProperty("type").GetString().Should().Be("Forum");
    }

    // ──────────── Create Forum Post ────────────

    [Fact]
    public async Task CreateForumPost_InForumChannel_Returns201WithPostDetails()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/posts",
            token,
            new { title = "My First Post", content = "Hello, forum!", tags = new List<string>() });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("threadId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("conversationId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("channelId").ReadLong().Should().Be(channelId);
        body.GetProperty("title").GetString().Should().Be("My First Post");
        body.GetProperty("firstMessageId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateForumPost_WithTags_ReturnsPostWithTags()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/posts",
            token,
            new { title = "Tagged Post", content = "Post with tags", tags = new[] { "help", "bug" } });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Tagged Post");
        var tags = body.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList();
        tags.Should().Contain("help");
        tags.Should().Contain("bug");
    }

    [Fact]
    public async Task CreateForumPost_InTextChannel_ReturnsBadRequest()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId, "text-channel", type: 0);
        var channelId = channel.GetProperty("id").ReadLong();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/posts",
            owner.AccessToken,
            new { title = "Should Fail", content = "Not a forum", tags = new List<string>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateForumPost_NonMember_Returns403()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();
        var outsider = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/posts",
            outsider.AccessToken,
            new { title = "Outsider Post", content = "Should fail", tags = new List<string>() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── List Forum Posts ────────────

    [Fact]
    public async Task ListForumPosts_ReturnsAllPosts()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        await CreateForumPostAsync(token, channelId, "Post One", "Content one");
        await CreateForumPostAsync(token, channelId, "Post Two", "Content two");

        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/posts",
            token);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var posts = body.GetProperty("posts").EnumerateArray().ToList();
        posts.Should().HaveCountGreaterThanOrEqualTo(2);

        var titles = posts.Select(p => p.GetProperty("title").GetString()).ToList();
        titles.Should().Contain("Post One");
        titles.Should().Contain("Post Two");
    }

    [Fact]
    public async Task ListForumPosts_SortByCreationDate_ReturnsSortedPosts()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var first = await CreateForumPostAsync(token, channelId, "First Post", "Created first");
        var second = await CreateForumPostAsync(token, channelId, "Second Post", "Created second");

        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/posts?sort=creation_date",
            token);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var posts = body.GetProperty("posts").EnumerateArray().ToList();
        posts.Should().HaveCountGreaterThanOrEqualTo(2);

        // Sorted by creation date descending, so second post should be first
        var firstPostCreatedAt = posts[0].GetProperty("createdAt").GetDateTimeOffset();
        var secondPostCreatedAt = posts[1].GetProperty("createdAt").GetDateTimeOffset();
        firstPostCreatedAt.Should().BeOnOrAfter(secondPostCreatedAt);
    }

    [Fact]
    public async Task ListForumPosts_FilterByArchived_ReturnsOnlyArchived()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var post = await CreateForumPostAsync(token, channelId, "Post to Archive", "Will be archived");
        var threadId = post.GetProperty("threadId").ReadLong();

        // Archive the thread via update
        var archiveResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token,
            new { isArchived = true });

        archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // List only archived posts
        var listArchivedResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/posts?archived=true",
            token);

        listArchivedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var archivedBody = await listArchivedResponse.ReadAsJsonAsync<JsonElement>();
        var archivedPosts = archivedBody.GetProperty("posts").EnumerateArray().ToList();
        archivedPosts.Should().NotBeEmpty();
        foreach (var p in archivedPosts)
        {
            p.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        }

        // List only active posts should not include the archived one
        var listActiveResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/posts?archived=false",
            token);

        listActiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var activeBody = await listActiveResponse.ReadAsJsonAsync<JsonElement>();
        var activePosts = activeBody.GetProperty("posts").EnumerateArray().ToList();
        foreach (var p in activePosts)
        {
            p.GetProperty("isArchived").GetBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public async Task ListForumPosts_FilterByTag_ReturnsOnlyMatchingPosts()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        // Create a tag first
        var tag = await CreateForumTagAsync(token, channelId, "important");
        var tagId = tag.GetProperty("id").ReadLong();

        // Create two posts, only one with the tag
        var taggedPost = await CreateForumPostAsync(token, channelId, "Tagged Post", "Has tag", new List<string> { "important" });
        var untaggedPost = await CreateForumPostAsync(token, channelId, "Untagged Post", "No tag");

        var taggedThreadId = taggedPost.GetProperty("threadId").ReadLong();

        // List posts filtered by tag ID
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/posts?tag={tagId}",
            token);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var posts = body.GetProperty("posts").EnumerateArray().ToList();
        posts.Should().NotBeEmpty();
        posts.Should().Contain(p => p.GetProperty("threadId").ReadLong() == taggedThreadId);
        posts.Should().NotContain(p => p.GetProperty("title").GetString() == "Untagged Post");
    }

    [Fact]
    public async Task ListForumPosts_NonMember_Returns403()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();
        var outsider = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/posts",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Pin / Unpin (Archive/Unarchive) Forum Post ────────────
    // Note: Thread entity has IsArchived/IsLocked but no IsPinned field.
    // Pin/unpin is tested via the thread update endpoint (PATCH).

    // ──────────── Lock Forum Post ────────────

    [Fact]
    public async Task LockForumPost_AsOwner_SetsLocked()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var post = await CreateForumPostAsync(token, channelId, "Post to Lock", "Will be locked");
        var threadId = post.GetProperty("threadId").ReadLong();

        var lockResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token,
            new { isLocked = true });

        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await lockResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(threadId);
        body.GetProperty("isLocked").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UnlockForumPost_AsOwner_ClearsLocked()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var post = await CreateForumPostAsync(token, channelId, "Post to Unlock", "Will be locked then unlocked");
        var threadId = post.GetProperty("threadId").ReadLong();

        // Lock first
        var lockResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token,
            new { isLocked = true });

        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then unlock
        var unlockResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token,
            new { isLocked = false });

        unlockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await unlockResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(threadId);
        body.GetProperty("isLocked").GetBoolean().Should().BeFalse();
    }

    // ──────────── Archive Forum Post ────────────

    [Fact]
    public async Task ArchiveForumPost_AsOwner_SetsArchived()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var post = await CreateForumPostAsync(token, channelId, "Post to Archive", "Will be archived");
        var threadId = post.GetProperty("threadId").ReadLong();

        var archiveResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token,
            new { isArchived = true });

        archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await archiveResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(threadId);
        body.GetProperty("isArchived").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UnarchiveForumPost_AsOwner_ClearsArchived()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var post = await CreateForumPostAsync(token, channelId, "Post to Unarchive", "Will be archived then unarchived");
        var threadId = post.GetProperty("threadId").ReadLong();

        // Archive first
        await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token,
            new { isArchived = true });

        // Then unarchive
        var unarchiveResponse = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token,
            new { isArchived = false });

        unarchiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await unarchiveResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(threadId);
        body.GetProperty("isArchived").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ArchiveAndLockForumPost_AsOwner_SetsBothFlags()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var post = await CreateForumPostAsync(token, channelId, "Post to Archive and Lock", "Both flags");
        var threadId = post.GetProperty("threadId").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            token,
            new { isArchived = true, isLocked = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        body.GetProperty("isLocked").GetBoolean().Should().BeTrue();
    }

    // ──────────── Forum Tags (CRUD) ────────────

    [Fact]
    public async Task CreateForumTag_AsOwner_ReturnsTag()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/tags",
            token,
            new { name = "discussion" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("channelId").ReadLong().Should().Be(channelId);
        body.GetProperty("name").GetString().Should().Be("discussion");
        body.GetProperty("isModerated").GetBoolean().Should().BeFalse();
        body.GetProperty("position").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task CreateForumTag_WithEmoji_ReturnsTagWithEmoji()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/tags",
            token,
            new { name = "bug-report", emojiUnicode = "\U0001F41B" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("bug-report");
        body.GetProperty("emojiUnicode").GetString().Should().Be("\U0001F41B");
    }

    [Fact]
    public async Task UpdateForumTag_ChangeName_ReturnsUpdated()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var tag = await CreateForumTagAsync(token, channelId, "old-name");
        var tagId = tag.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/tags/{tagId}",
            token,
            new { name = "new-name" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(tagId);
        body.GetProperty("name").GetString().Should().Be("new-name");
    }

    [Fact]
    public async Task UpdateForumTag_SetModerated_ReturnsUpdated()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var tag = await CreateForumTagAsync(token, channelId, "mod-tag");
        var tagId = tag.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/tags/{tagId}",
            token,
            new { isModerated = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("isModerated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DeleteForumTag_AsOwner_Returns204()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var tag = await CreateForumTagAsync(token, channelId, "tag-to-delete");
        var tagId = tag.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{channelId}/tags/{tagId}",
            token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify tag no longer appears in list
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/tags",
            token);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var tags = body.GetProperty("tags").EnumerateArray().ToList();
        tags.Should().NotContain(t => t.GetProperty("id").ReadLong() == tagId);
    }

    [Fact]
    public async Task ListForumTags_ReturnsAllTags()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        await CreateForumTagAsync(token, channelId, "tag-alpha");
        await CreateForumTagAsync(token, channelId, "tag-beta");

        var response = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/tags",
            token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var tags = body.GetProperty("tags").EnumerateArray().ToList();
        tags.Should().HaveCountGreaterThanOrEqualTo(2);

        var tagNames = tags.Select(t => t.GetProperty("name").GetString()).ToList();
        tagNames.Should().Contain("tag-alpha");
        tagNames.Should().Contain("tag-beta");
    }

    // ──────────── Forum Post Tags (Add / Remove) ────────────

    [Fact]
    public async Task AddPostTag_AsPostAuthor_Returns204()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        // Create a tag
        var tag = await CreateForumTagAsync(token, channelId, "needs-review");
        var tagId = tag.GetProperty("id").ReadLong();

        // Create a post without the tag
        var post = await CreateForumPostAsync(token, channelId, "Post for Tagging", "Tag me");
        var threadId = post.GetProperty("threadId").ReadLong();

        // Add tag to post via PUT
        var request = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/channels/{channelId}/posts/{threadId}/tags/{tagId}",
            token);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddPostTag_DuplicateTag_ReturnsBadRequest()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        // Create tag and post with that tag
        var tag = await CreateForumTagAsync(token, channelId, "duplicate-tag");
        var tagId = tag.GetProperty("id").ReadLong();

        var post = await CreateForumPostAsync(token, channelId, "Post with Tag", "Has tag", new List<string> { "duplicate-tag" });
        var threadId = post.GetProperty("threadId").ReadLong();

        // Try to add the same tag again
        var request = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/channels/{channelId}/posts/{threadId}/tags/{tagId}",
            token);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemovePostTag_AsPostAuthor_Returns204()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        // Create tag and post with that tag
        var tag = await CreateForumTagAsync(token, channelId, "removable-tag");
        var tagId = tag.GetProperty("id").ReadLong();

        var post = await CreateForumPostAsync(token, channelId, "Post to Untag", "Remove my tag", new List<string> { "removable-tag" });
        var threadId = post.GetProperty("threadId").ReadLong();

        // Remove tag from post
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{channelId}/posts/{threadId}/tags/{tagId}",
            token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemovePostTag_NotApplied_Returns404()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        // Create a tag and a post without it
        var tag = await CreateForumTagAsync(token, channelId, "unapplied-tag");
        var tagId = tag.GetProperty("id").ReadLong();

        var post = await CreateForumPostAsync(token, channelId, "Post without Tag", "No tag here");
        var threadId = post.GetProperty("threadId").ReadLong();

        // Try to remove a tag that was never applied
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{channelId}/posts/{threadId}/tags/{tagId}",
            token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddPostTag_VerifyInListing_ShowsTagOnPost()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var tag = await CreateForumTagAsync(token, channelId, "verified-tag");
        var tagId = tag.GetProperty("id").ReadLong();

        var post = await CreateForumPostAsync(token, channelId, "Post for Tag Verification", "Check tags in listing");
        var threadId = post.GetProperty("threadId").ReadLong();

        // Add tag
        var addRequest = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/channels/{channelId}/posts/{threadId}/tags/{tagId}",
            token);
        var addResponse = await _fixture.Client.SendAsync(addRequest);
        addResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // List posts and verify tag is present
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/posts",
            token);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var posts = body.GetProperty("posts").EnumerateArray().ToList();
        var matchingPost = posts.FirstOrDefault(p => p.GetProperty("threadId").ReadLong() == threadId);
        matchingPost.ValueKind.Should().NotBe(JsonValueKind.Undefined);

        var postTags = matchingPost.GetProperty("tags").EnumerateArray()
            .Select(t => t.GetString()).ToList();
        postTags.Should().Contain("verified-tag");
    }

    // ──────────── Role Checks (Non-Owner Access) ────────────

    [Fact]
    public async Task CreateForumTag_AsNonOwnerMember_Returns403()
    {
        var (serverId, channelId, owner, member) = await SetupServerWithForumAndMember();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/tags",
            member.AccessToken,
            new { name = "member-tag" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateForumTag_AsNonOwnerMember_Returns403()
    {
        var (serverId, channelId, owner, member) = await SetupServerWithForumAndMember();

        var tag = await CreateForumTagAsync(owner.AccessToken, channelId, "owner-tag");
        var tagId = tag.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/tags/{tagId}",
            member.AccessToken,
            new { name = "member-renamed" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteForumTag_AsNonOwnerMember_Returns403()
    {
        var (serverId, channelId, owner, member) = await SetupServerWithForumAndMember();

        var tag = await CreateForumTagAsync(owner.AccessToken, channelId, "protected-tag");
        var tagId = tag.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{channelId}/tags/{tagId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LockForumPost_AsNonOwnerMember_Returns403()
    {
        var (serverId, channelId, owner, member) = await SetupServerWithForumAndMember();

        var post = await CreateForumPostAsync(owner.AccessToken, channelId, "Owner Post", "Should not be lockable by member");
        var threadId = post.GetProperty("threadId").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            member.AccessToken,
            new { isLocked = true });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ArchiveForumPost_AsNonOwnerMember_Returns403()
    {
        var (serverId, channelId, owner, member) = await SetupServerWithForumAndMember();

        var post = await CreateForumPostAsync(owner.AccessToken, channelId, "Owner Post", "Should not be archivable by member");
        var threadId = post.GetProperty("threadId").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/channels/{channelId}/threads/{threadId}",
            member.AccessToken,
            new { isArchived = true });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddPostTag_AsNonAuthorNonMod_Returns403()
    {
        var (serverId, channelId, owner, member) = await SetupServerWithForumAndMember();

        // Owner creates tag and post
        var tag = await CreateForumTagAsync(owner.AccessToken, channelId, "owner-only-tag");
        var tagId = tag.GetProperty("id").ReadLong();

        var post = await CreateForumPostAsync(owner.AccessToken, channelId, "Owner Post", "Only author can tag");
        var threadId = post.GetProperty("threadId").ReadLong();

        // Member (non-author, no ManageChannels) tries to add a tag
        var request = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/channels/{channelId}/posts/{threadId}/tags/{tagId}",
            member.AccessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemovePostTag_AsNonAuthorNonMod_Returns403()
    {
        var (serverId, channelId, owner, member) = await SetupServerWithForumAndMember();

        // Owner creates tag and post with that tag
        var tag = await CreateForumTagAsync(owner.AccessToken, channelId, "protected-post-tag");
        var tagId = tag.GetProperty("id").ReadLong();

        var post = await CreateForumPostAsync(owner.AccessToken, channelId, "Owner Tagged Post", "Protected", new List<string> { "protected-post-tag" });
        var threadId = post.GetProperty("threadId").ReadLong();

        // Member (non-author, no ManageChannels) tries to remove the tag
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{channelId}/posts/{threadId}/tags/{tagId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateForumPost_Unauthenticated_Returns401()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var response = await _fixture.Client.PostJsonAsync(
            $"/api/v1/channels/{channelId}/posts",
            new { title = "Unauthenticated", content = "Should fail", tags = new List<string>() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListForumPosts_Unauthenticated_Returns401()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var response = await _fixture.Client.GetAsync($"/api/v1/channels/{channelId}/posts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Forum Post Listing Details ────────────

    [Fact]
    public async Task ListForumPosts_ContainsExpectedFields()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        var post = await CreateForumPostAsync(token, channelId, "Detailed Post", "Check all the fields");
        var threadId = post.GetProperty("threadId").ReadLong();

        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/posts",
            token);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var posts = body.GetProperty("posts").EnumerateArray().ToList();
        var matchingPost = posts.First(p => p.GetProperty("threadId").ReadLong() == threadId);

        matchingPost.GetProperty("threadId").ReadLong().Should().BeGreaterThan(0);
        matchingPost.GetProperty("conversationId").ReadLong().Should().BeGreaterThan(0);
        matchingPost.GetProperty("channelId").ReadLong().Should().Be(channelId);
        matchingPost.GetProperty("title").GetString().Should().Be("Detailed Post");
        matchingPost.GetProperty("firstMessagePreview").GetString().Should().Be("Check all the fields");
        matchingPost.GetProperty("messageCount").GetInt32().Should().Be(1);
        matchingPost.GetProperty("isArchived").GetBoolean().Should().BeFalse();
        matchingPost.GetProperty("isLocked").GetBoolean().Should().BeFalse();
        matchingPost.GetProperty("lastActivityAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        matchingPost.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
    }

    // ──────────── Member Can Create Forum Post ────────────

    [Fact]
    public async Task CreateForumPost_AsMember_Returns201()
    {
        var (serverId, channelId, owner, member) = await SetupServerWithForumAndMember();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/channels/{channelId}/posts",
            member.AccessToken,
            new { title = "Member Post", content = "Posted by member", tags = new List<string>() });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("threadId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("title").GetString().Should().Be("Member Post");
    }

    [Fact]
    public async Task ListForumTags_AsMember_Returns200()
    {
        var (serverId, channelId, owner, member) = await SetupServerWithForumAndMember();

        // Owner creates a tag
        await CreateForumTagAsync(owner.AccessToken, channelId, "visible-tag");

        // Member should be able to list tags
        var response = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/tags",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var tags = body.GetProperty("tags").EnumerateArray().ToList();
        tags.Should().Contain(t => t.GetProperty("name").GetString() == "visible-tag");
    }

    // ──────────── Delete Forum Tag Cascades Post Tag Removal ────────────

    [Fact]
    public async Task DeleteForumTag_RemovesTagFromExistingPosts()
    {
        var (serverId, channelId, token) = await SetupServerWithForumChannel();

        // Create a tag
        var tag = await CreateForumTagAsync(token, channelId, "ephemeral-tag");
        var tagId = tag.GetProperty("id").ReadLong();

        // Create a post with that tag
        var post = await CreateForumPostAsync(token, channelId, "Post with Ephemeral Tag", "Will lose tag", new List<string> { "ephemeral-tag" });
        var threadId = post.GetProperty("threadId").ReadLong();

        // Delete the tag
        var deleteResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/channels/{channelId}/tags/{tagId}",
            token);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the post no longer has the tag
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/channels/{channelId}/posts",
            token);

        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var posts = body.GetProperty("posts").EnumerateArray().ToList();
        var matchingPost = posts.First(p => p.GetProperty("threadId").ReadLong() == threadId);
        var tags = matchingPost.GetProperty("tags").EnumerateArray()
            .Select(t => t.GetString()).ToList();
        tags.Should().NotContain("ephemeral-tag");
    }
}
