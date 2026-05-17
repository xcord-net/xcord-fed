using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Tests for new monetization features: default groups on server creation,
/// default tiers, invite-based group auto-assignment, and GroupLimitService.
/// </summary>
[Collection("WebApp")]
public class DefaultGroupsAndTiersTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public DefaultGroupsAndTiersTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Default Groups on Server Creation ────────────

    [Fact]
    public async Task CreateServer_CreatesDefaultGroups()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var groups = (await response.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();

        // Should have at least @everyone + Member + Moderator + Bot + Pro + VIP = 6
        // (Pro/VIP created because CanUseMemberTiers defaults to true)
        groups.Count.Should().BeGreaterThanOrEqualTo(6);

        var everyone = groups.First(g => g.GetProperty("isEveryone").GetBoolean());
        everyone.GetProperty("name").GetString().Should().Be("@everyone");
        everyone.GetProperty("position").GetInt32().Should().Be(0);

        var member = groups.First(g => g.GetProperty("name").GetString() == "Member");
        member.GetProperty("position").GetInt32().Should().Be(1);

        var moderator = groups.First(g => g.GetProperty("name").GetString() == "Moderator");
        moderator.GetProperty("position").GetInt32().Should().Be(2);
        moderator.GetProperty("color").GetString().Should().Be("#e06a8a");

        var bot = groups.First(g => g.GetProperty("name").GetString() == "Bot");
        bot.GetProperty("position").GetInt32().Should().Be(3);
        bot.GetProperty("color").GetString().Should().Be("#7289da");
    }

    [Fact]
    public async Task CreateServer_EveryoneGroupHasExpectedRoles()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await response.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();

        var everyone = groups.First(g => g.GetProperty("isEveryone").GetBoolean());
        var roles = everyone.GetProperty("roles").ReadLong();

        // @everyone should have: ViewChannels, SendMessages, EmbedLinks, AttachFiles,
        // ReadMessageHistory, AddReactions, Connect, Speak, CreatePublicThreads, SendMessagesInThreads
        roles.Should().NotBe(0, "@everyone should have default roles set");
        // Verify SendMessages (1 << 8 = 256) is included
        (roles & 256).Should().NotBe(0, "@everyone should have SendMessages");
        // Verify ViewChannels (1 << 0 = 1) is included
        (roles & 1).Should().NotBe(0, "@everyone should have ViewChannels");
    }

    [Fact]
    public async Task CreateServer_ModeratorGroupIncludesManageMessages()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await response.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();

        var moderator = groups.First(g => g.GetProperty("name").GetString() == "Moderator");
        var roles = moderator.GetProperty("roles").ReadLong();

        // ManageMessages = 1 << 7 = 128
        (roles & 128).Should().NotBe(0, "Moderator group should have ManageMessages");
        // KickMembers = 1 << 4 = 16
        (roles & 16).Should().NotBe(0, "Moderator group should have KickMembers");
        // BanMembers = 1 << 5 = 32
        (roles & 32).Should().NotBe(0, "Moderator group should have BanMembers");
    }

    // ──────────── Default Tiers on Server Creation ────────────

    [Fact]
    public async Task CreateServer_CreatesDefaultTiers_WhenMonetizationEnabled()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/tiers", owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var tiers = body.GetProperty("tiers").EnumerateArray().ToList();

        tiers.Should().HaveCount(3);

        tiers[0].GetProperty("name").GetString().Should().Be("Supporter");
        tiers[0].GetProperty("priceMonthly").GetInt32().Should().Be(499);
        tiers[0].GetProperty("position").GetInt32().Should().Be(0);

        tiers[1].GetProperty("name").GetString().Should().Be("Pro");
        tiers[1].GetProperty("priceMonthly").GetInt32().Should().Be(999);
        tiers[1].GetProperty("position").GetInt32().Should().Be(1);

        tiers[2].GetProperty("name").GetString().Should().Be("VIP");
        tiers[2].GetProperty("priceMonthly").GetInt32().Should().Be(1999);
        tiers[2].GetProperty("position").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task CreateServer_DefaultTiersReferenceValidGroups()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Get all groups
        var groupsResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await groupsResponse.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var groupIds = groups.Select(g => g.GetProperty("id").ReadLong()).ToHashSet();

        // Get all tiers
        var tiersResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/tiers", owner.AccessToken);
        var tiers = (await tiersResponse.ReadAsJsonAsync<JsonElement>()).GetProperty("tiers").EnumerateArray().ToList();

        foreach (var tier in tiers)
        {
            var tierGroupIds = tier.GetProperty("groupIds").EnumerateArray()
                .Select(e => e.ReadLong()).ToList();

            foreach (var gid in tierGroupIds)
            {
                groupIds.Should().Contain(gid,
                    $"tier '{tier.GetProperty("name").GetString()}' references group {gid} which should exist on the server");
            }
        }
    }

    [Fact]
    public async Task CreateServer_ProAndVipGroupsHaveLimitsJson()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await response.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();

        var proGroup = groups.First(g => g.GetProperty("name").GetString() == "Pro");
        proGroup.TryGetProperty("limitsJson", out var proLimits).Should().BeTrue();
        proLimits.GetString().Should().Contain("CreatePrivateThreads");

        var vipGroup = groups.First(g => g.GetProperty("name").GetString() == "VIP");
        vipGroup.TryGetProperty("limitsJson", out var vipLimits).Should().BeTrue();
        vipLimits.GetString().Should().Contain("CreateEncryptedChannel");
        vipLimits.GetString().Should().Contain("MaxFileUploadMb");
    }

    [Fact]
    public async Task CreateServer_SupporterTierGrantsMemberGroup()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Get Member group ID
        var groupsResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await groupsResponse.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var memberGroupId = groups.First(g => g.GetProperty("name").GetString() == "Member")
            .GetProperty("id").ReadLong();

        // Get Supporter tier
        var tiersResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/tiers", owner.AccessToken);
        var tiers = (await tiersResponse.ReadAsJsonAsync<JsonElement>()).GetProperty("tiers").EnumerateArray().ToList();
        var supporter = tiers.First(t => t.GetProperty("name").GetString() == "Supporter");

        var tierGroupIds = supporter.GetProperty("groupIds").EnumerateArray()
            .Select(e => e.ReadLong()).ToList();

        tierGroupIds.Should().Contain(memberGroupId, "Supporter tier should grant the Member group");
        tierGroupIds.Should().HaveCount(1, "Supporter tier should only grant Member group");
    }

    [Fact]
    public async Task CreateServer_VipTierGrantsAllThreeGroups()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Get group IDs
        var groupsResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await groupsResponse.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var memberGroupId = groups.First(g => g.GetProperty("name").GetString() == "Member").GetProperty("id").ReadLong();
        var proGroupId = groups.First(g => g.GetProperty("name").GetString() == "Pro").GetProperty("id").ReadLong();
        var vipGroupId = groups.First(g => g.GetProperty("name").GetString() == "VIP").GetProperty("id").ReadLong();

        // Get VIP tier
        var tiersResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/tiers", owner.AccessToken);
        var tiers = (await tiersResponse.ReadAsJsonAsync<JsonElement>()).GetProperty("tiers").EnumerateArray().ToList();
        var vipTier = tiers.First(t => t.GetProperty("name").GetString() == "VIP");

        var tierGroupIds = vipTier.GetProperty("groupIds").EnumerateArray()
            .Select(e => e.ReadLong()).ToList();

        tierGroupIds.Should().HaveCount(3, "VIP tier should grant Member + Pro + VIP groups");
        tierGroupIds.Should().Contain(memberGroupId);
        tierGroupIds.Should().Contain(proGroupId);
        tierGroupIds.Should().Contain(vipGroupId);
    }

    // ──────────── Invite Group Auto-Assignment ────────────

    [Fact]
    public async Task CreateInvite_WithGroupId_ReturnsGroupIdInResponse()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Get the Member group ID (non-@everyone)
        var groupsResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await groupsResponse.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var memberGroupId = groups.First(g => g.GetProperty("name").GetString() == "Member")
            .GetProperty("id").ReadLong();

        // Create invite with groupId
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/invites",
            owner.AccessToken,
            new { groupId = memberGroupId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var invite = await response.ReadAsJsonAsync<JsonElement>();
        invite.GetProperty("groupId").ReadLong().Should().Be(memberGroupId);
    }

    [Fact]
    public async Task CreateInvite_WithEveryoneGroupId_Returns404()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Get @everyone group ID
        var groupsResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await groupsResponse.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var everyoneGroupId = groups.First(g => g.GetProperty("isEveryone").GetBoolean())
            .GetProperty("id").ReadLong();

        // Create invite with @everyone - should be rejected
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/invites",
            owner.AccessToken,
            new { groupId = everyoneGroupId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "creating an invite with @everyone group should be rejected");
    }

    [Fact]
    public async Task CreateInvite_WithGroupId_RequiresManageGroups()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Get a group ID
        var groupsResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await groupsResponse.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var memberGroupId = groups.First(g => g.GetProperty("name").GetString() == "Member")
            .GetProperty("id").ReadLong();

        // Regular member (no ManageGroups) tries to create invite with groupId
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/invites",
            member.AccessToken,
            new { groupId = memberGroupId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "regular member without ManageGroups should not be able to create group-assigning invites");
    }

    [Fact]
    public async Task JoinByInvite_WithGroupId_AutoAssignsGroup()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Get the Member group ID
        var groupsResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await groupsResponse.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var memberGroupId = groups.First(g => g.GetProperty("name").GetString() == "Member")
            .GetProperty("id").ReadLong();

        // Create invite with group auto-assignment
        var inviteResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/invites",
            owner.AccessToken,
            new { groupId = memberGroupId });
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invite = await inviteResponse.ReadAsJsonAsync<JsonElement>();
        var code = invite.GetProperty("code").GetString()!;

        // New user joins via this invite
        var joiner = await _helper.RegisterUserAsync();
        var joinResponse = await _helper.JoinServerAsync(joiner.AccessToken, code);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the group was auto-assigned
        var memberResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/members/{joiner.UserId}",
            joiner.AccessToken);
        memberResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var memberData = await memberResponse.ReadAsJsonAsync<JsonElement>();
        var assignedGroupIds = memberData.GetProperty("groupIds").EnumerateArray()
            .Select(e => e.ReadLong()).ToList();

        assignedGroupIds.Should().Contain(memberGroupId,
            "joining via an invite with groupId should auto-assign that group");
    }

    [Fact]
    public async Task JoinByInvite_WithoutGroupId_DoesNotAssignGroups()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create regular invite (no groupId)
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);

        // New user joins
        var joiner = await _helper.RegisterUserAsync();
        await _helper.JoinServerAsync(joiner.AccessToken, inviteCode);

        // Verify no groups were assigned
        var memberResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{serverId}/members/{joiner.UserId}",
            joiner.AccessToken);
        var memberData = await memberResponse.ReadAsJsonAsync<JsonElement>();
        var assignedGroupIds = memberData.GetProperty("groupIds").EnumerateArray()
            .Select(e => e.ReadLong()).ToList();

        assignedGroupIds.Should().BeEmpty(
            "joining via a regular invite should not assign any groups");
    }

    // ──────────── GroupLimitService (via DB) ────────────

    [Fact]
    public async Task GroupLimitService_ResolvesMaxLimitAcrossGroups()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Get the Pro and VIP group IDs (both have LimitsJson)
        var groupsResponse = await _helper.AuthGetAsync($"/api/v1/servers/{serverId}/groups", owner.AccessToken);
        var groups = (await groupsResponse.ReadAsJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var proGroupId = groups.First(g => g.GetProperty("name").GetString() == "Pro").GetProperty("id").ReadLong();
        var vipGroupId = groups.First(g => g.GetProperty("name").GetString() == "VIP").GetProperty("id").ReadLong();

        // Create a member and assign both Pro and VIP groups
        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{proGroupId}",
            owner.AccessToken);
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{vipGroupId}",
            owner.AccessToken);

        // Resolve limits through the service directly via DB context
        await using var db = _fixture.CreateDbContext();
        var limitService = new Xcord.Infrastructure.Services.GroupLimitService(db);

        // Pro has {"CreatePrivateThreads": 5}, VIP has {"CreateEncryptedChannel": 3, "MaxFileUploadMb": 50}
        var privateThreadsLimit = await limitService.GetEffectiveLimit(member.UserId, serverId, "CreatePrivateThreads");
        privateThreadsLimit.Should().Be(5);

        var encryptedChannelLimit = await limitService.GetEffectiveLimit(member.UserId, serverId, "CreateEncryptedChannel");
        encryptedChannelLimit.Should().Be(3);

        var uploadLimit = await limitService.GetEffectiveLimit(member.UserId, serverId, "MaxFileUploadMb");
        uploadLimit.Should().Be(50);
    }

    [Fact]
    public async Task GroupLimitService_ReturnsNull_WhenNoLimitsDefined()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Member with no special groups - only @everyone applies (no LimitsJson)
        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        await using var db = _fixture.CreateDbContext();
        var limitService = new Xcord.Infrastructure.Services.GroupLimitService(db);

        var result = await limitService.GetEffectiveLimit(member.UserId, serverId, "CreatePrivateThreads");
        result.Should().BeNull("member without any groups that define limits should get null");
    }

    [Fact]
    public async Task GroupLimitService_TakesMaximumAcrossGroups()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create two custom groups with different limits for the same key
        var group1Response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "LimitLow", color = "#111111", roles = 0, position = 10 });
        var group1 = await group1Response.ReadAsJsonAsync<JsonElement>();
        var group1Id = group1.GetProperty("id").ReadLong();

        var group2Response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "LimitHigh", color = "#222222", roles = 0, position = 11 });
        var group2 = await group2Response.ReadAsJsonAsync<JsonElement>();
        var group2Id = group2.GetProperty("id").ReadLong();

        // Set LimitsJson directly via DB (not exposed via API)
        await using (var db = _fixture.CreateDbContext())
        {
            var g1 = await db.Groups.FindAsync(group1Id);
            g1!.LimitsJson = """{"MaxChannels": 3}""";
            var g2 = await db.Groups.FindAsync(group2Id);
            g2!.LimitsJson = """{"MaxChannels": 10}""";
            await db.SaveChangesAsync();
        }

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Assign both groups
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{group1Id}",
            owner.AccessToken);
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{group2Id}",
            owner.AccessToken);

        await using var db2 = _fixture.CreateDbContext();
        var limitService = new Xcord.Infrastructure.Services.GroupLimitService(db2);

        var result = await limitService.GetEffectiveLimit(member.UserId, serverId, "MaxChannels");
        result.Should().Be(10, "should return the maximum value across all groups (most permissive wins)");
    }

    [Fact]
    public async Task GroupLimitService_IncludesEveryoneGroupLimits()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Set LimitsJson on @everyone directly via DB
        await using (var db = _fixture.CreateDbContext())
        {
            var everyone = db.Groups.First(g => g.ServerId == serverId && g.IsEveryone);
            everyone.LimitsJson = """{"MaxDmChannels": 5}""";
            await db.SaveChangesAsync();
        }

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        await using var db2 = _fixture.CreateDbContext();
        var limitService = new Xcord.Infrastructure.Services.GroupLimitService(db2);

        var result = await limitService.GetEffectiveLimit(member.UserId, serverId, "MaxDmChannels");
        result.Should().Be(5, "@everyone's LimitsJson should be included in limit resolution");
    }

    [Fact]
    public async Task GroupLimitService_SkipsIncompatibleJsonStructure()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        // Create a group and set LimitsJson to valid JSON that isn't a Dictionary<string, int>
        // (jsonb column requires valid JSON, but the service should handle type mismatches)
        var groupResponse = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            owner.AccessToken,
            new { name = "WeirdJson", color = "#333333", roles = 0, position = 10 });
        var group = await groupResponse.ReadAsJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").ReadLong();

        await using (var db = _fixture.CreateDbContext())
        {
            // Set to a JSON array instead of object - valid jsonb but can't deserialize to Dictionary<string, int>
            var g = await db.Groups.FindAsync(groupId);
            g!.LimitsJson = """[1, 2, 3]""";
            await db.SaveChangesAsync();
        }

        var member = await _helper.RegisterUserAsync();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/members/{member.UserId}/groups/{groupId}",
            owner.AccessToken);

        await using var db2 = _fixture.CreateDbContext();
        var limitService = new Xcord.Infrastructure.Services.GroupLimitService(db2);

        // Should not throw, just return null (array can't deserialize to Dictionary<string, int>)
        var result = await limitService.GetEffectiveLimit(member.UserId, serverId, "SomeKey");
        result.Should().BeNull("incompatible JSON structure should be skipped gracefully");
    }
}
