using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class DmTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public DmTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Create DM ────────────

    [Fact]
    public async Task CreateDm_OneOnOne_Returns200()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            user1.AccessToken,
            new { recipientIds = new[] { user2.UserId } });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var dm = await response.ReadAsJsonAsync<JsonElement>();
        dm.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        dm.GetProperty("conversationId").ReadLong().Should().BeGreaterThan(0);
        dm.GetProperty("isGroup").GetBoolean().Should().BeFalse();
        dm.GetProperty("members").EnumerateArray().Should().HaveCount(2);

        // Verify both members are present
        var members = dm.GetProperty("members").EnumerateArray().ToList();
        var memberUserIds = members.Select(m => m.GetProperty("userId").ReadLong()).ToList();
        memberUserIds.Should().Contain(user1.UserId);
        memberUserIds.Should().Contain(user2.UserId);
    }

    [Fact]
    public async Task CreateDm_GroupDm_Returns200()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();
        var user3 = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            user1.AccessToken,
            new { recipientIds = new[] { user2.UserId, user3.UserId } });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var dm = await response.ReadAsJsonAsync<JsonElement>();
        dm.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        dm.GetProperty("conversationId").ReadLong().Should().BeGreaterThan(0);
        dm.GetProperty("isGroup").GetBoolean().Should().BeTrue();
        dm.GetProperty("ownerId").ReadLong().Should().Be(user1.UserId);
        dm.GetProperty("members").EnumerateArray().Should().HaveCount(3);

        // Verify all members are present
        var members = dm.GetProperty("members").EnumerateArray().ToList();
        var memberUserIds = members.Select(m => m.GetProperty("userId").ReadLong()).ToList();
        memberUserIds.Should().Contain(user1.UserId);
        memberUserIds.Should().Contain(user2.UserId);
        memberUserIds.Should().Contain(user3.UserId);
    }

    [Fact]
    public async Task CreateDm_SameRecipient_ReturnsSameChannel()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();

        // Create DM first time
        var response1 = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            user1.AccessToken,
            new { recipientIds = new[] { user2.UserId } });

        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        var dm1 = await response1.ReadAsJsonAsync<JsonElement>();
        var dmId1 = dm1.GetProperty("id").ReadLong();

        // Create DM second time with same recipient
        var response2 = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            user1.AccessToken,
            new { recipientIds = new[] { user2.UserId } });

        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        var dm2 = await response2.ReadAsJsonAsync<JsonElement>();
        var dmId2 = dm2.GetProperty("id").ReadLong();

        // Should return the same DM channel
        dmId2.Should().Be(dmId1);
    }

    // ──────────── List DMs ────────────

    [Fact]
    public async Task ListDms_ReturnsUserDms()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();

        // Create a DM
        var createResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            user1.AccessToken,
            new { recipientIds = new[] { user2.UserId } });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdDm = await createResponse.ReadAsJsonAsync<JsonElement>();
        var createdDmId = createdDm.GetProperty("id").ReadLong();

        // List DMs
        var listResponse = await _helper.AuthGetAsync("/api/v1/users/@me/dms", user1.AccessToken);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dms = await listResponse.ReadAsJsonAsync<JsonElement>();
        var dmArray = dms.GetProperty("dmChannels").EnumerateArray().ToList();

        dmArray.Should().HaveCountGreaterThanOrEqualTo(1);
        dmArray.Should().Contain(dm => dm.GetProperty("id").ReadLong() == createdDmId);
    }

    // ──────────── Get DM ────────────

    [Fact]
    public async Task GetDm_Exists_Returns200()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();

        // Create a DM
        var createResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            user1.AccessToken,
            new { recipientIds = new[] { user2.UserId } });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdDm = await createResponse.ReadAsJsonAsync<JsonElement>();
        var dmId = createdDm.GetProperty("id").ReadLong();

        // Get the specific DM
        var getResponse = await _helper.AuthGetAsync($"/api/v1/users/@me/dms/{dmId}", user1.AccessToken);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dm = await getResponse.ReadAsJsonAsync<JsonElement>();
        dm.GetProperty("id").ReadLong().Should().Be(dmId);
        dm.GetProperty("conversationId").ReadLong().Should().BeGreaterThan(0);
        dm.GetProperty("members").EnumerateArray().Should().HaveCount(2);
    }

    // ──────────── Send Message in DM ────────────

    [Fact]
    public async Task SendMessageInDm_Succeeds()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();

        // Create a DM
        var createResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            user1.AccessToken,
            new { recipientIds = new[] { user2.UserId } });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var dm = await createResponse.ReadAsJsonAsync<JsonElement>();
        var conversationId = dm.GetProperty("conversationId").ReadLong();

        // Send a message in the DM using the conversation endpoint
        var message = await _helper.SendMessageAsync(user1.AccessToken, conversationId, "Hello in DM!");

        message.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        message.GetProperty("conversationId").ReadLong().Should().Be(conversationId);
        message.GetProperty("authorId").ReadLong().Should().Be(user1.UserId);
        message.GetProperty("content").GetString().Should().Be("Hello in DM!");
    }

    // ──────────── Group DM Management ────────────

    [Fact]
    public async Task AddGroupDmMember_Returns204()
    {
        var owner = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();
        var user3 = await _helper.RegisterUserAsync();
        var user4 = await _helper.RegisterUserAsync();

        // Create a group DM (need 2+ recipients to make it a group DM)
        var createResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            owner.AccessToken,
            new { recipientIds = new[] { user2.UserId, user3.UserId } });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var dm = await createResponse.ReadAsJsonAsync<JsonElement>();
        var dmId = dm.GetProperty("id").ReadLong();
        dm.GetProperty("isGroup").GetBoolean().Should().BeTrue("DM with 2+ recipients should be a group DM");

        // Add fourth user to the group DM
        var request = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/@me/dms/{dmId}/members/{user4.UserId}",
            owner.AccessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the member was added by fetching the DM
        var getResponse = await _helper.AuthGetAsync($"/api/v1/users/@me/dms/{dmId}", owner.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedDm = await getResponse.ReadAsJsonAsync<JsonElement>();
        var members = updatedDm.GetProperty("members").EnumerateArray().ToList();
        members.Should().HaveCount(4);

        var memberUserIds = members.Select(m => m.GetProperty("userId").ReadLong()).ToList();
        memberUserIds.Should().Contain(user4.UserId);
    }

    [Fact]
    public async Task RemoveGroupDmMember_Returns204()
    {
        var owner = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();
        var user3 = await _helper.RegisterUserAsync();

        // Create a group DM with 3 members
        var createResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            owner.AccessToken,
            new { recipientIds = new[] { user2.UserId, user3.UserId } });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var dm = await createResponse.ReadAsJsonAsync<JsonElement>();
        var dmId = dm.GetProperty("id").ReadLong();

        // Remove user3 from the group DM
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/users/@me/dms/{dmId}/members/{user3.UserId}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the member was removed by fetching the DM
        var getResponse = await _helper.AuthGetAsync($"/api/v1/users/@me/dms/{dmId}", owner.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedDm = await getResponse.ReadAsJsonAsync<JsonElement>();
        var members = updatedDm.GetProperty("members").EnumerateArray().ToList();
        members.Should().HaveCount(2);

        var memberUserIds = members.Select(m => m.GetProperty("userId").ReadLong()).ToList();
        memberUserIds.Should().NotContain(user3.UserId);
    }

    // ──────────── Leave DM ────────────

    [Fact]
    public async Task LeaveDm_Returns204()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();

        // Create a DM
        var createResponse = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            user1.AccessToken,
            new { recipientIds = new[] { user2.UserId } });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var dm = await createResponse.ReadAsJsonAsync<JsonElement>();
        var dmId = dm.GetProperty("id").ReadLong();

        // Leave the DM
        var response = await _helper.AuthDeleteAsync($"/api/v1/users/@me/dms/{dmId}", user1.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify user1 can no longer see the DM in their list
        var listResponse = await _helper.AuthGetAsync("/api/v1/users/@me/dms", user1.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dms = await listResponse.ReadAsJsonAsync<JsonElement>();
        var dmArray = dms.GetProperty("dmChannels").EnumerateArray().ToList();

        dmArray.Should().NotContain(d => d.GetProperty("id").ReadLong() == dmId);
    }
}
