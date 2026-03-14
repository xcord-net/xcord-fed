using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xcord.Entities;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class GroupRoleTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public GroupRoleTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    [Fact]
    public async Task ListGroups_AsMember_ReturnsGroupsWithEveryone()
    {
        // Arrange - create owner and server
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create a custom group as owner
        var createGroupResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "CustomTestGroup", color = "#FF5733", roles = 1024, position = 10 }
        );
        createGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Add a member to the server
        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Act - list groups as member
        var response = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", member.AccessToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var groups = await response.ReadAsJsonAsync<JsonElement>();
        var groupArray = groups.EnumerateArray().ToList();

        // Should have @everyone + default groups (Member, Moderator, Bot) + custom group
        groupArray.Count.Should().BeGreaterThanOrEqualTo(5);

        var everyoneGroup = groupArray.FirstOrDefault(r =>
            r.GetProperty("isEveryone").GetBoolean() == true);
        everyoneGroup.ValueKind.Should().NotBe(JsonValueKind.Undefined, "@everyone group should exist");

        var customGroup = groupArray.FirstOrDefault(r =>
            r.GetProperty("name").GetString() == "CustomTestGroup");
        customGroup.ValueKind.Should().NotBe(JsonValueKind.Undefined, "custom group should exist");
        customGroup.GetProperty("color").GetString().Should().Be("#FF5733");
        customGroup.GetProperty("roles").ReadLong().Should().Be(1024);
        customGroup.GetProperty("position").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task CreateGroup_AsOwner_Returns201WithGroupDetails()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Act - create group as owner (ManageGroups = 4 = 1 << 2)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new
            {
                name = "Administrator",
                color = "#FF0000",
                roles = 4611686018427387904, // Administrator = 1 << 62
                position = 5
            }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await response.ReadAsJsonAsync<JsonElement>();

        group.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        group.GetProperty("serverId").ReadLong().Should().Be(serverId);
        group.GetProperty("name").GetString().Should().Be("Administrator");
        group.GetProperty("color").GetString().Should().Be("#FF0000");
        group.GetProperty("roles").ReadLong().Should().Be(4611686018427387904);
        group.GetProperty("position").GetInt32().Should().Be(5);
        group.GetProperty("isEveryone").GetBoolean().Should().BeFalse();
        group.TryGetProperty("createdAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateGroup_AsNonOwner_Returns403()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Act - attempt to create group as non-owner without ManageGroups role
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            member.AccessToken,
            new { name = "Unauthorized", color = "#000000", roles = 0, position = 1 }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateGroup_AsOwner_ReturnsUpdatedGroup()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "OldName", color = "#AAAAAA", roles = 1024, position = 1 }
        );
        var createdGroup = await createResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = createdGroup.GetProperty("id").ReadLong();

        // Act - update group
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{serverId}/groups/{groupId}",
            owner.AccessToken,
            new
            {
                name = "NewName",
                color = "#BBBBBB",
                roles = 2048,
                position = 2
            }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedGroup = await response.ReadAsJsonAsync<JsonElement>();

        updatedGroup.GetProperty("id").ReadLong().Should().Be(groupId);
        updatedGroup.GetProperty("name").GetString().Should().Be("NewName");
        updatedGroup.GetProperty("color").GetString().Should().Be("#BBBBBB");
        updatedGroup.GetProperty("roles").ReadLong().Should().Be(2048);
        updatedGroup.GetProperty("position").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task UpdateGroup_AsNonOwner_Returns403()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "Group", color = "#FFFFFF", roles = 0, position = 1 }
        );
        var group = await createResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Act - attempt update as non-owner
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{serverId}/groups/{groupId}",
            member.AccessToken,
            new { name = "Hacked" }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteGroup_AsOwner_Returns204()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "ToDelete", color = "#000000", roles = 0, position = 1 }
        );
        var group = await createResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        // Act - delete group
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/groups/{groupId}",
            owner.AccessToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify group no longer appears in list
        var listResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = await listResponse.ReadAsJsonAsync<JsonElement>();
        var groupArray = groups.EnumerateArray().ToList();

        groupArray.Should().NotContain(r =>
            r.GetProperty("id").ReadLong() == groupId);
    }

    [Fact]
    public async Task DeleteGroup_EveryoneGroup_Returns400()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Get the @everyone group ID
        var listResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = await listResponse.ReadAsJsonAsync<JsonElement>();
        var everyoneGroup = groups.EnumerateArray().First(r =>
            r.GetProperty("isEveryone").GetBoolean() == true);
        var everyoneGroupId = everyoneGroup.GetProperty("id").ReadLong();

        // Act - attempt to delete @everyone group
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/groups/{everyoneGroupId}",
            owner.AccessToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteGroup_AsNonOwner_Returns403()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "Protected", color = "#000000", roles = 0, position = 1 }
        );
        var group = await createResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Act - attempt delete as non-owner
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/groups/{groupId}",
            member.AccessToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignGroup_ToMember_Returns204()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "Moderator", color = "#00FF00", roles = 262144, position = 1 }
        );
        var group = await createResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Act - assign group to member
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken
        );

        // Assert - 204 returned
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the group assignment persisted by fetching the member and checking their group list
        var memberResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}",
            owner.AccessToken
        );
        memberResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var memberData = await memberResponse.ReadAsJsonAsync<JsonElement>();
        var groupIds = memberData.GetProperty("groupIds").EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? long.Parse(e.GetString()!) : e.GetInt64())
            .ToList();
        groupIds.Should().Contain(groupId, "the assigned group should appear in the member's group list");
    }

    [Fact]
    public async Task AssignGroup_AlreadyAssigned_Returns409()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "Group", color = "#111111", roles = 0, position = 1 }
        );
        var group = await createResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Assign group first time
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken
        );

        // Act - assign same group again
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveGroup_FromMember_Returns204()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "Temporary", color = "#222222", roles = 0, position = 1 }
        );
        var group = await createResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Assign group first
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken
        );

        // Act - remove group from member
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken
        );

        // Assert - 204 returned
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the group removal persisted by fetching the member and confirming the group is gone
        var memberResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}",
            owner.AccessToken
        );
        memberResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var memberData = await memberResponse.ReadAsJsonAsync<JsonElement>();
        var groupIds = memberData.GetProperty("groupIds").EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? long.Parse(e.GetString()!) : e.GetInt64())
            .ToList();
        groupIds.Should().NotContain(groupId, "the removed group should no longer appear in the member's group list");
    }

    [Fact]
    public async Task RoleEnforcement_MemberWithManageChannels_CanCreateChannel()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create group with ManageChannels role (1 << 3 = 8)
        var createGroupResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "ChannelManager", color = "#0000FF", roles = 8, position = 1 }
        );
        var group = await createGroupResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Verify member cannot create channel without group
        var unauthorizedResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "unauthorized-channel", type = 0 }
        );
        unauthorizedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assign group with ManageChannels role
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken
        );

        // Act - member with group should now be able to create channel
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "authorized-channel", type = 0 }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var channel = await response.ReadAsJsonAsync<JsonElement>();
        channel.GetProperty("name").GetString().Should().Be("authorized-channel");
        channel.GetProperty("serverId").ReadLong().Should().Be(serverId);
    }

    // -- Cache Invalidation Tests --
    // These tests prove that role cache entries are actively invalidated
    // (not merely expired) when groups are changed.  The 60-second cache TTL means
    // a stale entry would persist for a full minute if invalidation were broken;
    // by asserting the new state IMMEDIATELY after the group change we distinguish
    // active invalidation from passive expiry.

    [Fact]
    public async Task CacheInvalidation_AssignGroup_GrantsAccessImmediately()
    {
        // Arrange: create server + group with ManageChannels (bit 1 = 2)
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createGroupResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "CacheTestGroup", color = "#CCCCCC", roles = (long)Role.ManageChannels, position = 1 }
        );
        createGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await createGroupResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Populate the role cache with the member's CURRENT (no group) state by
        // making an access attempt.  The server will evaluate and cache: no ManageChannels.
        var deniedBefore = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "before-group-channel", type = 0 }
        );
        deniedBefore.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "member should not have ManageChannels before group is assigned");

        // Act: assign the group - handler must invalidate the cached denial
        var assignResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken
        );
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: IMMEDIATELY verify access is granted.
        // If invalidation were broken the stale "denied" cache entry would still be in Redis
        // for up to 60 seconds and this request would return 403.
        var grantedAfter = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "after-group-channel", type = 0 }
        );
        grantedAfter.StatusCode.Should().Be(HttpStatusCode.Created,
            "assign-group handler must invalidate the role cache so the new role takes effect immediately");
    }

    [Fact]
    public async Task CacheInvalidation_UpdateGroupRemovesRole_DeniesAccessImmediately()
    {
        // Arrange: create server + group with ManageChannels
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createGroupResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "CacheRevocationGroup", color = "#DDDDDD", roles = (long)Role.ManageChannels, position = 1 }
        );
        createGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await createGroupResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Assign group so the member has ManageChannels
        var assignResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken
        );
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Populate the role cache with the ALLOWED state by making a successful request.
        var allowedBefore = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "before-update-channel", type = 0 }
        );
        allowedBefore.StatusCode.Should().Be(HttpStatusCode.Created,
            "member should have ManageChannels after group assignment");

        // Act: update the group to strip ManageChannels (set roles = 0)
        var updateResponse = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{serverId}/groups/{groupId}",
            owner.AccessToken,
            new { roles = 0L }
        );
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: IMMEDIATELY verify access is denied.
        // If invalidation were broken the stale "allowed" cache entry would persist for up
        // to 60 seconds and this request would return 201 instead of 403.
        var deniedAfter = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "after-update-channel", type = 0 }
        );
        deniedAfter.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "update-group handler must invalidate the role cache so the revoked role takes effect immediately");
    }

    [Fact]
    public async Task CacheInvalidation_RemoveGroup_DeniesAccessImmediately()
    {
        // Arrange: create server + group with ManageChannels
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createGroupResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "CacheRemovalGroup", color = "#EEEEEE", roles = (long)Role.ManageChannels, position = 1 }
        );
        createGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await createGroupResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Assign group so the member has ManageChannels
        var assignResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken
        );
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Populate the role cache with the ALLOWED state
        var allowedBefore = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "before-remove-group-channel", type = 0 }
        );
        allowedBefore.StatusCode.Should().Be(HttpStatusCode.Created,
            "member should have ManageChannels after group assignment");

        // Act: remove the group from the member - handler must invalidate the cached grant
        var removeResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken
        );
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: IMMEDIATELY verify access is denied.
        // If invalidation were broken the stale "allowed" cache entry would persist for up
        // to 60 seconds and this request would return 201 instead of 403.
        var deniedAfter = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "after-remove-group-channel", type = 0 }
        );
        deniedAfter.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "remove-group handler must invalidate the role cache so the revoked role takes effect immediately");
    }
}
