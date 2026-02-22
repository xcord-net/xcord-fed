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
public class RolePermissionTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public RolePermissionTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    [Fact]
    public async Task ListRoles_AsMember_ReturnsRolesWithEveryone()
    {
        // Arrange - create owner and server
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create a custom role as owner
        var createRoleResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "Moderator", color = "#FF5733", permissions = 1024, position = 1 }
        );
        createRoleResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Add a member to the server
        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Act - list roles as member
        var response = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/roles", member.AccessToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var roles = await response.ReadAsJsonAsync<JsonElement>();
        var roleArray = roles.EnumerateArray().ToList();

        // Should have @everyone role + custom Moderator role
        roleArray.Count.Should().BeGreaterThanOrEqualTo(2);

        var everyoneRole = roleArray.FirstOrDefault(r =>
            r.GetProperty("isEveryone").GetBoolean() == true);
        everyoneRole.ValueKind.Should().NotBe(JsonValueKind.Undefined, "@everyone role should exist");

        var moderatorRole = roleArray.FirstOrDefault(r =>
            r.GetProperty("name").GetString() == "Moderator");
        moderatorRole.ValueKind.Should().NotBe(JsonValueKind.Undefined, "custom role should exist");
        moderatorRole.GetProperty("color").GetString().Should().Be("#FF5733");
        moderatorRole.GetProperty("permissions").ReadLong().Should().Be(1024);
        moderatorRole.GetProperty("position").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task CreateRole_AsOwner_Returns201WithRoleDetails()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Act - create role as owner (ManageRoles = 4 = 1 << 2)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new
            {
                name = "Administrator",
                color = "#FF0000",
                permissions = 4611686018427387904, // Administrator = 1 << 62
                position = 5
            }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var role = await response.ReadAsJsonAsync<JsonElement>();

        role.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        role.GetProperty("serverId").ReadLong().Should().Be(serverId);
        role.GetProperty("name").GetString().Should().Be("Administrator");
        role.GetProperty("color").GetString().Should().Be("#FF0000");
        role.GetProperty("permissions").ReadLong().Should().Be(4611686018427387904);
        role.GetProperty("position").GetInt32().Should().Be(5);
        role.GetProperty("isEveryone").GetBoolean().Should().BeFalse();
        role.TryGetProperty("createdAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateRole_AsNonOwner_Returns403()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Act - attempt to create role as non-owner without ManageRoles permission
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            member.AccessToken,
            new { name = "Unauthorized", color = "#000000", permissions = 0, position = 1 }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateRole_AsOwner_ReturnsUpdatedRole()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "OldName", color = "#AAAAAA", permissions = 1024, position = 1 }
        );
        var createdRole = await createResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = createdRole.GetProperty("id").ReadLong();

        // Act - update role
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{serverId}/roles/{roleId}",
            owner.AccessToken,
            new
            {
                name = "NewName",
                color = "#BBBBBB",
                permissions = 2048,
                position = 2
            }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedRole = await response.ReadAsJsonAsync<JsonElement>();

        updatedRole.GetProperty("id").ReadLong().Should().Be(roleId);
        updatedRole.GetProperty("name").GetString().Should().Be("NewName");
        updatedRole.GetProperty("color").GetString().Should().Be("#BBBBBB");
        updatedRole.GetProperty("permissions").ReadLong().Should().Be(2048);
        updatedRole.GetProperty("position").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task UpdateRole_AsNonOwner_Returns403()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "Role", color = "#FFFFFF", permissions = 0, position = 1 }
        );
        var role = await createResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Act - attempt update as non-owner
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{serverId}/roles/{roleId}",
            member.AccessToken,
            new { name = "Hacked" }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteRole_AsOwner_Returns204()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "ToDelete", color = "#000000", permissions = 0, position = 1 }
        );
        var role = await createResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        // Act - delete role
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/roles/{roleId}",
            owner.AccessToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify role no longer appears in list
        var listResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/roles", owner.AccessToken);
        var roles = await listResponse.ReadAsJsonAsync<JsonElement>();
        var roleArray = roles.EnumerateArray().ToList();

        roleArray.Should().NotContain(r =>
            r.GetProperty("id").ReadLong() == roleId);
    }

    [Fact]
    public async Task DeleteRole_EveryoneRole_Returns400()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Get the @everyone role ID
        var listResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/roles", owner.AccessToken);
        var roles = await listResponse.ReadAsJsonAsync<JsonElement>();
        var everyoneRole = roles.EnumerateArray().First(r =>
            r.GetProperty("isEveryone").GetBoolean() == true);
        var everyoneRoleId = everyoneRole.GetProperty("id").ReadLong();

        // Act - attempt to delete @everyone role
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/roles/{everyoneRoleId}",
            owner.AccessToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteRole_AsNonOwner_Returns403()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "Protected", color = "#000000", permissions = 0, position = 1 }
        );
        var role = await createResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Act - attempt delete as non-owner
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/roles/{roleId}",
            member.AccessToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignRole_ToMember_Returns204()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "Moderator", color = "#00FF00", permissions = 262144, position = 1 }
        );
        var role = await createResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Act - assign role to member
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/roles/{roleId}",
            owner.AccessToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignRole_AlreadyAssigned_Returns409()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "Role", color = "#111111", permissions = 0, position = 1 }
        );
        var role = await createResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Assign role first time
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/roles/{roleId}",
            owner.AccessToken
        );

        // Act - assign same role again
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/roles/{roleId}",
            owner.AccessToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveRole_FromMember_Returns204()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "Temporary", color = "#222222", permissions = 0, position = 1 }
        );
        var role = await createResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Assign role first
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/roles/{roleId}",
            owner.AccessToken
        );

        // Act - remove role from member
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/roles/{roleId}",
            owner.AccessToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PermissionEnforcement_MemberWithManageChannels_CanCreateChannel()
    {
        // Arrange
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create role with ManageChannels permission (1 << 3 = 8)
        var createRoleResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "ChannelManager", color = "#0000FF", permissions = 8, position = 1 }
        );
        var role = await createRoleResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Verify member cannot create channel without role
        var unauthorizedResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "unauthorized-channel", type = 0 }
        );
        unauthorizedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assign role with ManageChannels permission
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/roles/{roleId}",
            owner.AccessToken
        );

        // Act - member with role should now be able to create channel
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

    // ── Cache Invalidation Tests ──────────────────────────────────────────────
    // These tests prove that permission cache entries are actively invalidated
    // (not merely expired) when roles are changed.  The 60-second cache TTL means
    // a stale entry would persist for a full minute if invalidation were broken;
    // by asserting the new state IMMEDIATELY after the role change we distinguish
    // active invalidation from passive expiry.

    [Fact]
    public async Task CacheInvalidation_AssignRole_GrantsAccessImmediately()
    {
        // Arrange: create server + role with ManageChannels (bit 1 = 2)
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createRoleResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "CacheTestRole", color = "#CCCCCC", permissions = (long)Permission.ManageChannels, position = 1 }
        );
        createRoleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var role = await createRoleResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Populate the permission cache with the member's CURRENT (no role) state by
        // making an access attempt.  The server will evaluate and cache: no ManageChannels.
        var deniedBefore = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "before-role-channel", type = 0 }
        );
        deniedBefore.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "member should not have ManageChannels before role is assigned");

        // Act: assign the role — handler must invalidate the cached denial
        var assignResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/roles/{roleId}",
            owner.AccessToken
        );
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: IMMEDIATELY verify access is granted.
        // If invalidation were broken the stale "denied" cache entry would still be in Redis
        // for up to 60 seconds and this request would return 403.
        var grantedAfter = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "after-role-channel", type = 0 }
        );
        grantedAfter.StatusCode.Should().Be(HttpStatusCode.Created,
            "assign-role handler must invalidate the permission cache so the new permission takes effect immediately");
    }

    [Fact]
    public async Task CacheInvalidation_UpdateRoleRemovesPermission_DeniesAccessImmediately()
    {
        // Arrange: create server + role with ManageChannels
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createRoleResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "CacheRevocationRole", color = "#DDDDDD", permissions = (long)Permission.ManageChannels, position = 1 }
        );
        createRoleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var role = await createRoleResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Assign role so the member has ManageChannels
        var assignResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/roles/{roleId}",
            owner.AccessToken
        );
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Populate the permission cache with the ALLOWED state by making a successful request.
        var allowedBefore = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "before-update-channel", type = 0 }
        );
        allowedBefore.StatusCode.Should().Be(HttpStatusCode.Created,
            "member should have ManageChannels after role assignment");

        // Act: update the role to strip ManageChannels (set permissions = 0)
        var updateResponse = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{serverId}/roles/{roleId}",
            owner.AccessToken,
            new { permissions = 0L }
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
            "update-role handler must invalidate the permission cache so the revoked permission takes effect immediately");
    }

    [Fact]
    public async Task CacheInvalidation_RemoveRole_DeniesAccessImmediately()
    {
        // Arrange: create server + role with ManageChannels
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var createRoleResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/roles",
            owner.AccessToken,
            new { name = "CacheRemovalRole", color = "#EEEEEE", permissions = (long)Permission.ManageChannels, position = 1 }
        );
        createRoleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var role = await createRoleResponse.ReadAsJsonAsync<JsonElement>();
        var roleId = role.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Assign role so the member has ManageChannels
        var assignResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/roles/{roleId}",
            owner.AccessToken
        );
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Populate the permission cache with the ALLOWED state
        var allowedBefore = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "before-remove-role-channel", type = 0 }
        );
        allowedBefore.StatusCode.Should().Be(HttpStatusCode.Created,
            "member should have ManageChannels after role assignment");

        // Act: remove the role from the member — handler must invalidate the cached grant
        var removeResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/roles/{roleId}",
            owner.AccessToken
        );
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: IMMEDIATELY verify access is denied.
        // If invalidation were broken the stale "allowed" cache entry would persist for up
        // to 60 seconds and this request would return 201 instead of 403.
        var deniedAfter = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/channels",
            member.AccessToken,
            new { name = "after-remove-role-channel", type = 0 }
        );
        deniedAfter.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "remove-role handler must invalidate the permission cache so the revoked permission takes effect immediately");
    }
}
