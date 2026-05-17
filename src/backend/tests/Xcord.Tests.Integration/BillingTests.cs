using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class BillingTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public BillingTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helper Methods ────────────

    private async Task<BillingTestContext> SetupAsync()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        return new BillingTestContext(owner, serverId);
    }

    private async Task<JsonElement> CreateTierAsync(
        string accessToken, long serverId,
        string name = "Premium", int priceMonthly = 500, long[]? groupIds = null)
    {
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/tiers",
            accessToken,
            new
            {
                name,
                description = $"Description for {name}",
                priceMonthly,
                currency = "usd",
                groupIds = groupIds ?? Array.Empty<long>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"tier creation should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    private async Task<long> CreateGroupAsync(string accessToken, long serverId, string name = "Subscriber")
    {
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/groups",
            accessToken,
            new { name, color = "#00FF00", roles = 0, position = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var group = await response.ReadAsJsonAsync<JsonElement>();
        return group.GetProperty("id").ReadLong();
    }

    private sealed record BillingTestContext(AuthenticatedUser Owner, long ServerId);

    // ──────────── List Subscription Tiers ────────────

    [Fact]
    public async Task ListTiers_NewServer_ReturnsDefaultTiers()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var tiers = body.GetProperty("tiers").EnumerateArray().ToList();
        tiers.Should().HaveCount(3, "new server should have 3 default tiers (Supporter, Pro, VIP)");

        tiers[0].GetProperty("name").GetString().Should().Be("Supporter");
        tiers[1].GetProperty("name").GetString().Should().Be("Pro");
        tiers[2].GetProperty("name").GetString().Should().Be("VIP");
    }

    [Fact]
    public async Task ListTiers_WithTiers_ReturnsOrderedByPosition()
    {
        var ctx = await SetupAsync();

        // Create two tiers (on top of the 3 defaults: Supporter, Pro, VIP)
        await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Bronze", 300);
        await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Silver", 700);

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var tiers = body.GetProperty("tiers").EnumerateArray().ToList();
        tiers.Should().HaveCount(5, "3 defaults + 2 custom tiers");

        // Defaults at positions 0, 1, 2
        tiers[0].GetProperty("name").GetString().Should().Be("Supporter");
        tiers[1].GetProperty("name").GetString().Should().Be("Pro");
        tiers[2].GetProperty("name").GetString().Should().Be("VIP");

        // Custom tiers at positions 3, 4
        tiers[3].GetProperty("name").GetString().Should().Be("Bronze");
        tiers[3].GetProperty("priceMonthly").GetInt32().Should().Be(300);
        tiers[3].GetProperty("position").GetInt32().Should().Be(3);

        tiers[4].GetProperty("name").GetString().Should().Be("Silver");
        tiers[4].GetProperty("priceMonthly").GetInt32().Should().Be(700);
        tiers[4].GetProperty("position").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task ListTiers_OnlyReturnsActiveTiers()
    {
        var ctx = await SetupAsync();

        // Create a tier then deactivate it
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Deactivated", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            ctx.Owner.AccessToken,
            new { isActive = false });

        // Create an active tier
        await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Active", 300);

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var tiers = body.GetProperty("tiers").EnumerateArray().ToList();
        tiers.Should().HaveCount(4, "3 defaults + 1 active custom (deactivated excluded)");
        tiers.Should().NotContain(t => t.GetProperty("name").GetString() == "Deactivated");
        tiers.Should().Contain(t => t.GetProperty("name").GetString() == "Active");
    }

    [Fact]
    public async Task ListTiers_NonExistentServer_Returns404()
    {
        var owner = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/servers/999999999999/tiers",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListTiers_Unauthenticated_Returns401()
    {
        var ctx = await SetupAsync();

        var response = await _fixture.Client.GetAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Create Subscription Tier ────────────

    [Fact]
    public async Task CreateTier_AsOwner_Returns201WithTierDetails()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken,
            new
            {
                name = "Gold",
                description = "Gold tier benefits",
                priceMonthly = 999,
                currency = "usd",
                groupIds = Array.Empty<long>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);
        body.GetProperty("name").GetString().Should().Be("Gold");
        body.GetProperty("description").GetString().Should().Be("Gold tier benefits");
        body.GetProperty("priceMonthly").GetInt32().Should().Be(999);
        body.GetProperty("currency").GetString().Should().Be("usd");
        body.GetProperty("isActive").GetBoolean().Should().BeTrue();
        body.GetProperty("position").GetInt32().Should().Be(3, "3 default tiers at positions 0-2");
    }

    [Fact]
    public async Task CreateTier_WithGroupIds_ReturnsTierWithGroups()
    {
        var ctx = await SetupAsync();
        var groupId = await CreateGroupAsync(ctx.Owner.AccessToken, ctx.ServerId, "Subscriber");

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken,
            new
            {
                name = "Group Tier",
                priceMonthly = 500,
                currency = "usd",
                groupIds = new[] { groupId }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var groupIds = body.GetProperty("groupIds").EnumerateArray()
            .Select(r => r.ReadLong()).ToList();
        groupIds.Should().Contain(groupId);
    }

    [Fact]
    public async Task CreateTier_PriceTooLow_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken,
            new
            {
                name = "Cheap",
                priceMonthly = 50, // Below $1.00 minimum (100 cents)
                currency = "usd",
                groupIds = Array.Empty<long>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTier_PriceTooHigh_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken,
            new
            {
                name = "Expensive",
                priceMonthly = 200_000, // Above $1000.00 maximum (100000 cents)
                currency = "usd",
                groupIds = Array.Empty<long>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTier_EmptyName_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken,
            new
            {
                name = "",
                priceMonthly = 500,
                currency = "usd",
                groupIds = Array.Empty<long>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTier_NameTooLong_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken,
            new
            {
                name = new string('A', 101), // Exceeds 100-character limit
                priceMonthly = 500,
                currency = "usd",
                groupIds = Array.Empty<long>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTier_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            member.AccessToken,
            new
            {
                name = "Forbidden Tier",
                priceMonthly = 500,
                currency = "usd",
                groupIds = Array.Empty<long>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateTier_NonExistentServer_Returns404()
    {
        var owner = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/servers/999999999999/tiers",
            owner.AccessToken,
            new
            {
                name = "Orphaned",
                priceMonthly = 500,
                currency = "usd",
                groupIds = Array.Empty<long>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTier_DefaultCurrency_UsesUsd()
    {
        var ctx = await SetupAsync();

        // Omit currency field to test default
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken,
            new
            {
                name = "Default Currency",
                priceMonthly = 500,
                groupIds = Array.Empty<long>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("currency").GetString().Should().Be("usd");
    }

    [Fact]
    public async Task CreateTier_PositionAutoIncrements()
    {
        var ctx = await SetupAsync();

        // 3 default tiers at positions 0, 1, 2
        var tier1 = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Tier One", 300);
        var tier2 = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Tier Two", 500);
        var tier3 = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Tier Three", 800);

        tier1.GetProperty("position").GetInt32().Should().Be(3);
        tier2.GetProperty("position").GetInt32().Should().Be(4);
        tier3.GetProperty("position").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task CreateTier_MaxTiersLimit_Returns400()
    {
        var ctx = await SetupAsync();

        // 3 default tiers already exist, create 7 more to reach the limit of 10
        for (var i = 0; i < 7; i++)
        {
            await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, $"Tier {i}", 100 + i * 100);
        }

        // The 11th (3 defaults + 7 + 1) should fail
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken,
            new
            {
                name = "Overflow Tier",
                priceMonthly = 500,
                currency = "usd",
                groupIds = Array.Empty<long>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Update Subscription Tier ────────────

    [Fact]
    public async Task UpdateTier_Name_ReturnsUpdatedTier()
    {
        var ctx = await SetupAsync();
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Original");
        var tierId = tier.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            ctx.Owner.AccessToken,
            new { name = "Renamed" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(tierId);
        body.GetProperty("name").GetString().Should().Be("Renamed");
    }

    [Fact]
    public async Task UpdateTier_Price_ReturnsUpdatedPrice()
    {
        var ctx = await SetupAsync();
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Price Test", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            ctx.Owner.AccessToken,
            new { priceMonthly = 999 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("priceMonthly").GetInt32().Should().Be(999);
    }

    [Fact]
    public async Task UpdateTier_Deactivate_ReturnsInactive()
    {
        var ctx = await SetupAsync();
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Active Tier", 500);
        var tierId = tier.GetProperty("id").ReadLong();
        tier.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            ctx.Owner.AccessToken,
            new { isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTier_Reactivate_ReturnsActive()
    {
        var ctx = await SetupAsync();
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Toggle Tier", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        // Deactivate
        await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            ctx.Owner.AccessToken,
            new { isActive = false });

        // Reactivate
        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            ctx.Owner.AccessToken,
            new { isActive = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTier_GroupIds_ReturnsUpdatedGroups()
    {
        var ctx = await SetupAsync();
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Group Tier", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        var groupId = await CreateGroupAsync(ctx.Owner.AccessToken, ctx.ServerId, "New Group");

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            ctx.Owner.AccessToken,
            new { groupIds = new[] { groupId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var groupIds = body.GetProperty("groupIds").EnumerateArray()
            .Select(r => r.ReadLong()).ToList();
        groupIds.Should().Contain(groupId);
    }

    [Fact]
    public async Task UpdateTier_AsNonOwner_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Protected", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            member.AccessToken,
            new { name = "Hacked" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateTier_NonExistentTier_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/999999999999",
            ctx.Owner.AccessToken,
            new { name = "Ghost" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Delete Subscription Tier ────────────

    [Fact]
    public async Task DeleteTier_AsOwner_Returns200()
    {
        var ctx = await SetupAsync();
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Delete Me", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it no longer appears in the active tier list
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers",
            ctx.Owner.AccessToken);

        var body = await listResponse.ReadAsJsonAsync<JsonElement>();
        var tiers = body.GetProperty("tiers").EnumerateArray()
            .Where(t => t.GetProperty("id").ReadLong() == tierId)
            .ToList();

        tiers.Should().BeEmpty("soft-deleted tier should not appear in active listing");
    }

    [Fact]
    public async Task DeleteTier_AsNonOwner_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Protected Tier", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteTier_NonExistentTier_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/999999999999",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Subscribe (No Stripe / Dev Mode) ────────────

    [Fact]
    public async Task Subscribe_AsMember_ReturnsActiveSubscription()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Owner creates a tier
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Premium", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        // Member subscribes
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("requiresCheckout").GetBoolean().Should().BeFalse(
            "no Stripe configured in test environment, subscription should be applied directly");
        body.TryGetProperty("checkoutUrl", out var checkoutUrl);
        (checkoutUrl.ValueKind == JsonValueKind.Null || checkoutUrl.ValueKind == JsonValueKind.Undefined)
            .Should().BeTrue("checkoutUrl should be null without Stripe");

        var subscription = body.GetProperty("subscription");
        subscription.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        subscription.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);
        subscription.GetProperty("tierId").ReadLong().Should().Be(tierId);
        subscription.GetProperty("tierName").GetString().Should().Be("Premium");
        subscription.GetProperty("priceMonthly").GetInt32().Should().Be(500);
        subscription.GetProperty("status").GetString().Should().Be("Active");
        subscription.GetProperty("currentPeriodEnd").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Subscribe_AlreadySubscribed_Returns400()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Duplicate Test", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        // First subscription
        var first = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second subscription attempt
        var second = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Subscribe_NonMember_Returns403()
    {
        var ctx = await SetupAsync();
        var nonMember = await _helper.RegisterUserAsync();

        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Exclusive", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        // Non-member attempts to subscribe (has not joined the server)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            nonMember.AccessToken,
            new { tierId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Subscribe_InactiveTier_Returns404()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Create and deactivate a tier
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Inactive Tier", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/tiers/{tierId}",
            ctx.Owner.AccessToken,
            new { isActive = false });

        // Attempt to subscribe to inactive tier
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Subscribe_NonExistentTier_Returns404()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId = 999999999999L });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Subscribe_NonExistentServer_Returns404()
    {
        var member = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/servers/999999999999/subscribe",
            member.AccessToken,
            new { tierId = 1L });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Subscribe_WithTierGroups_AssignsGroupsAutomatically()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Create a group and a tier that grants that group
        var groupId = await CreateGroupAsync(ctx.Owner.AccessToken, ctx.ServerId, "Subscriber Group");
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Group Tier", 500, new[] { groupId });
        var tierId = tier.GetProperty("id").ReadLong();

        // Member subscribes
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the group was auto-assigned by checking the member's server profile
        var memberResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/members/{member.UserId}",
            member.AccessToken);

        memberResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var memberBody = await memberResponse.ReadAsJsonAsync<JsonElement>();
        var memberGroupIds = memberBody.GetProperty("groupIds").EnumerateArray()
            .Select(r => r.ReadLong()).ToList();
        memberGroupIds.Should().Contain(groupId,
            "subscribing to a tier with groups should auto-assign those groups");
    }

    // ──────────── Get Member Subscription ────────────

    [Fact]
    public async Task GetSubscription_ActiveSubscription_ReturnsDetails()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "View Test", 799);
        var tierId = tier.GetProperty("id").ReadLong();

        // Subscribe
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });

        // Get subscription
        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);
        body.GetProperty("tierId").ReadLong().Should().Be(tierId);
        body.GetProperty("tierName").GetString().Should().Be("View Test");
        body.GetProperty("priceMonthly").GetInt32().Should().Be(799);
        body.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task GetSubscription_NoSubscription_Returns404()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSubscription_Unauthenticated_Returns401()
    {
        var ctx = await SetupAsync();

        var response = await _fixture.Client.GetAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Cancel Subscription ────────────

    [Fact]
    public async Task CancelSubscription_ActiveSubscription_Returns200()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Cancel Test", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        // Subscribe
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });

        // Cancel
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription/cancel",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify subscription is no longer active
        var getResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription",
            member.AccessToken);

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "cancelled subscription should not appear as active");
    }

    [Fact]
    public async Task CancelSubscription_NoSubscription_Returns404()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription/cancel",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelSubscription_RemovesTierGroups()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        // Create group + tier that grants it
        var groupId = await CreateGroupAsync(ctx.Owner.AccessToken, ctx.ServerId, "Cancel Group Test");
        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Group Cancel", 500, new[] { groupId });
        var tierId = tier.GetProperty("id").ReadLong();

        // Subscribe (should auto-assign group)
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });

        // Verify group was assigned
        var memberBefore = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/members/{member.UserId}",
            member.AccessToken);
        var beforeBody = await memberBefore.ReadAsJsonAsync<JsonElement>();
        beforeBody.GetProperty("groupIds").EnumerateArray()
            .Select(r => r.ReadLong()).Should().Contain(groupId);

        // Cancel subscription (should remove group)
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription/cancel",
            member.AccessToken);

        // Verify group was removed
        var memberAfter = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/members/{member.UserId}",
            member.AccessToken);
        var afterBody = await memberAfter.ReadAsJsonAsync<JsonElement>();
        afterBody.GetProperty("groupIds").EnumerateArray()
            .Select(r => r.ReadLong()).Should().NotContain(groupId,
            "tier groups should be removed when subscription is cancelled");
    }

    [Fact]
    public async Task CancelSubscription_AlreadyCancelled_Returns404()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Double Cancel", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        // Subscribe and cancel
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });

        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription/cancel",
            member.AccessToken);

        // Attempt to cancel again
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription/cancel",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "already cancelled subscription should not be found as active");
    }

    [Fact]
    public async Task CancelSubscription_Unauthenticated_Returns401()
    {
        var ctx = await SetupAsync();

        var response = await _fixture.Client.PostJsonAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription/cancel",
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Resubscribe After Cancellation ────────────

    [Fact]
    public async Task Subscribe_AfterCancellation_AllowsNewSubscription()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var tier = await CreateTierAsync(ctx.Owner.AccessToken, ctx.ServerId, "Resub Test", 500);
        var tierId = tier.GetProperty("id").ReadLong();

        // Subscribe, then cancel
        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });

        await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscription/cancel",
            member.AccessToken);

        // Subscribe again
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/subscribe",
            member.AccessToken,
            new { tierId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("subscription").GetProperty("status").GetString().Should().Be("Active");
    }

    // ──────────── Server Billing Config (Owner Dashboard) ────────────

    [Fact]
    public async Task GetBillingConfig_AsOwner_ReturnsConfig()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/billing/dashboard",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);
        body.GetProperty("stripeConfigured").GetBoolean().Should().BeFalse(
            "no Stripe key configured in test environment");
    }

    [Fact]
    public async Task GetBillingConfig_AsNonOwner_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/billing/dashboard",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBillingConfig_NonExistentServer_Returns404()
    {
        var owner = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            "/api/v1/servers/999999999999/billing/dashboard",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBillingConfig_Unauthenticated_Returns401()
    {
        var ctx = await SetupAsync();

        var response = await _fixture.Client.GetAsync(
            $"/api/v1/servers/{ctx.ServerId}/billing/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
