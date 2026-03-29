using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class ScheduledEventTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public ScheduledEventTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helper Methods ────────────

    private async Task<EventTestContext> SetupAsync()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        return new EventTestContext(owner, serverId);
    }

    private sealed record EventTestContext(
        AuthenticatedUser Owner,
        long ServerId);

    private async Task<JsonElement> CreateEventAsync(
        string accessToken, long serverId,
        string? name = null, string? description = null, string? location = null,
        DateTimeOffset? scheduledStartTime = null, DateTimeOffset? scheduledEndTime = null)
    {
        name ??= $"Event_{Guid.NewGuid():N}"[..20];
        var startTime = scheduledStartTime ?? DateTimeOffset.UtcNow.AddDays(7);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{serverId}/events",
            accessToken,
            new
            {
                name,
                description,
                location,
                scheduledStartTime = startTime,
                scheduledEndTime
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"event creation should succeed: {await response.Content.ReadAsStringAsync()}");

        return await response.ReadAsJsonAsync<JsonElement>();
    }

    // ──────────── Create Event ────────────

    [Fact]
    public async Task CreateEvent_AsOwner_ReturnsEventDetails()
    {
        var ctx = await SetupAsync();
        var startTime = DateTimeOffset.UtcNow.AddDays(7);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            ctx.Owner.AccessToken,
            new
            {
                name = "Game Night",
                description = "Weekly game night event",
                location = "Voice Channel",
                scheduledStartTime = startTime,
                scheduledEndTime = startTime.AddHours(2)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("serverId").ReadLong().Should().Be(ctx.ServerId);
        body.GetProperty("creatorId").ReadLong().Should().Be(ctx.Owner.UserId);
        body.GetProperty("name").GetString().Should().Be("Game Night");
        body.GetProperty("description").GetString().Should().Be("Weekly game night event");
        body.GetProperty("location").GetString().Should().Be("Voice Channel");
        body.GetProperty("status").GetString().Should().Be("Scheduled");
        body.GetProperty("interestedCount").GetInt32().Should().Be(0);
        body.GetProperty("createdAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateEvent_WithMinimalFields_ReturnsEventDetails()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            ctx.Owner.AccessToken,
            new
            {
                name = "Minimal Event",
                scheduledStartTime = DateTimeOffset.UtcNow.AddDays(1)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Minimal Event");
        body.GetProperty("description").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("location").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task CreateEvent_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        // Member joins the server
        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var joinResponse = await _helper.JoinServerAsync(member.AccessToken, inviteCode);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Member attempts to create an event (should fail -- no ManageEvents permission)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            member.AccessToken,
            new
            {
                name = "Forbidden Event",
                scheduledStartTime = DateTimeOffset.UtcNow.AddDays(1)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateEvent_Unauthenticated_Returns401()
    {
        var ctx = await SetupAsync();

        var response = await _fixture.Client.PostJsonAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            new
            {
                name = "Unauthenticated Event",
                scheduledStartTime = DateTimeOffset.UtcNow.AddDays(1)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEvent_StartTimeInPast_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            ctx.Owner.AccessToken,
            new
            {
                name = "Past Event",
                scheduledStartTime = DateTimeOffset.UtcNow.AddDays(-1)
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_EndTimeBeforeStartTime_Returns400()
    {
        var ctx = await SetupAsync();
        var startTime = DateTimeOffset.UtcNow.AddDays(7);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            ctx.Owner.AccessToken,
            new
            {
                name = "Bad Timing Event",
                scheduledStartTime = startTime,
                scheduledEndTime = startTime.AddHours(-1)
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_EmptyName_Returns400()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            ctx.Owner.AccessToken,
            new
            {
                name = "",
                scheduledStartTime = DateTimeOffset.UtcNow.AddDays(1)
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Get Event ────────────

    [Fact]
    public async Task GetEvent_AsOwner_ReturnsEventDetails()
    {
        var ctx = await SetupAsync();
        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Get Me");
        var eventId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(eventId);
        body.GetProperty("name").GetString().Should().Be("Get Me");
    }

    [Fact]
    public async Task GetEvent_AsMember_ReturnsEventDetails()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Public Event");
        var eventId = created.GetProperty("id").ReadLong();

        // Member can view events (read-only -- no permission required beyond membership)
        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Public Event");
    }

    [Fact]
    public async Task GetEvent_NonExistent_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/999999999999",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetEvent_AsNonMember_Returns403()
    {
        var ctx = await SetupAsync();
        var outsider = await _helper.RegisterUserAsync();

        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var eventId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── List Events ────────────

    [Fact]
    public async Task ListEvents_ReturnsScheduledEvents()
    {
        var ctx = await SetupAsync();

        // Create two events
        await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Event Alpha");
        await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Event Beta");

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await response.ReadAsJsonAsync<JsonElement>();
        var list = events.EnumerateArray().ToList();
        list.Should().HaveCountGreaterThanOrEqualTo(2);

        var names = list.Select(e => e.GetProperty("name").GetString()).ToList();
        names.Should().Contain("Event Alpha");
        names.Should().Contain("Event Beta");
    }

    [Fact]
    public async Task ListEvents_DefaultsToScheduledStatus()
    {
        var ctx = await SetupAsync();

        await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Scheduled One");

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await response.ReadAsJsonAsync<JsonElement>();
        var list = events.EnumerateArray().ToList();

        // All returned events should have Scheduled status
        foreach (var evt in list)
        {
            evt.GetProperty("status").GetString().Should().Be("Scheduled");
        }
    }

    [Fact]
    public async Task ListEvents_AsNonMember_Returns403()
    {
        var ctx = await SetupAsync();
        var outsider = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Update Event ────────────

    [Fact]
    public async Task UpdateEvent_Name_ReturnsUpdatedEvent()
    {
        var ctx = await SetupAsync();
        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Original Name");
        var eventId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            ctx.Owner.AccessToken,
            new { name = "Updated Name" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(eventId);
        body.GetProperty("name").GetString().Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateEvent_Description_ReturnsUpdatedEvent()
    {
        var ctx = await SetupAsync();
        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Desc Event");
        var eventId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            ctx.Owner.AccessToken,
            new { description = "Updated description" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("description").GetString().Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdateEvent_Status_ReturnsUpdatedStatus()
    {
        var ctx = await SetupAsync();
        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Status Event");
        var eventId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            ctx.Owner.AccessToken,
            new { status = "Active" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task UpdateEvent_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var eventId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            member.AccessToken,
            new { name = "Forbidden Update" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateEvent_NonExistent_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthPatchAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/999999999999",
            ctx.Owner.AccessToken,
            new { name = "Ghost Event" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Delete Event ────────────

    [Fact]
    public async Task DeleteEvent_AsOwner_Returns204()
    {
        var ctx = await SetupAsync();
        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Delete Me");
        var eventId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it no longer appears in listings
        var listResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events",
            ctx.Owner.AccessToken);

        var events = await listResponse.ReadAsJsonAsync<JsonElement>();
        var remaining = events.EnumerateArray()
            .Where(e => e.GetProperty("id").ReadLong() == eventId)
            .ToList();

        remaining.Should().BeEmpty("deleted event should not appear in listing");
    }

    [Fact]
    public async Task DeleteEvent_AsNonOwnerMember_Returns403()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var eventId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteEvent_NonExistent_Returns404()
    {
        var ctx = await SetupAsync();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/999999999999",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── RSVP Event ────────────

    [Fact]
    public async Task RsvpEvent_AsMember_Returns204AndIncrementsCount()
    {
        var ctx = await SetupAsync();
        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "RSVP Event");
        var eventId = created.GetProperty("id").ReadLong();
        created.GetProperty("interestedCount").GetInt32().Should().Be(0);

        // RSVP
        var request = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}/rsvp", ctx.Owner.AccessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify interested count increased
        var getResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            ctx.Owner.AccessToken);

        var body = await getResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("interestedCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task RsvpEvent_Idempotent_DoesNotDoubleCount()
    {
        var ctx = await SetupAsync();
        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Idempotent RSVP");
        var eventId = created.GetProperty("id").ReadLong();

        // RSVP twice
        var request1 = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}/rsvp", ctx.Owner.AccessToken);
        await _fixture.Client.SendAsync(request1);

        var request2 = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}/rsvp", ctx.Owner.AccessToken);
        await _fixture.Client.SendAsync(request2);

        // Verify interested count is still 1 (idempotent)
        var getResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            ctx.Owner.AccessToken);

        var body = await getResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("interestedCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task RsvpEvent_MultipleUsers_IncreasesCount()
    {
        var ctx = await SetupAsync();
        var member = await _helper.RegisterUserAsync();

        var inviteCode = await _helper.CreateInviteAsync(ctx.Owner.AccessToken, ctx.ServerId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Multi RSVP");
        var eventId = created.GetProperty("id").ReadLong();

        // Both users RSVP
        var request1 = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}/rsvp", ctx.Owner.AccessToken);
        await _fixture.Client.SendAsync(request1);

        var request2 = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}/rsvp", member.AccessToken);
        await _fixture.Client.SendAsync(request2);

        // Verify count is 2
        var getResponse = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            ctx.Owner.AccessToken);

        var body = await getResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("interestedCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task RsvpEvent_AsNonMember_Returns403()
    {
        var ctx = await SetupAsync();
        var outsider = await _helper.RegisterUserAsync();

        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var eventId = created.GetProperty("id").ReadLong();

        var request = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}/rsvp", outsider.AccessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RsvpEvent_NonExistentEvent_Returns404()
    {
        var ctx = await SetupAsync();

        var request = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/servers/{ctx.ServerId}/events/999999999999/rsvp", ctx.Owner.AccessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Un-RSVP Event ────────────

    [Fact]
    public async Task UnrsvpEvent_AfterRsvp_DecrementsCount()
    {
        var ctx = await SetupAsync();
        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "Unrsvp Event");
        var eventId = created.GetProperty("id").ReadLong();

        // RSVP first
        var rsvpRequest = TestHelper.AuthRequest(HttpMethod.Put,
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}/rsvp", ctx.Owner.AccessToken);
        await _fixture.Client.SendAsync(rsvpRequest);

        // Verify count is 1
        var getResponse1 = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            ctx.Owner.AccessToken);
        var body1 = await getResponse1.ReadAsJsonAsync<JsonElement>();
        body1.GetProperty("interestedCount").GetInt32().Should().Be(1);

        // Un-RSVP
        var unrsvpResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}/rsvp",
            ctx.Owner.AccessToken);

        unrsvpResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify count is back to 0
        var getResponse2 = await _helper.AuthGetAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}",
            ctx.Owner.AccessToken);
        var body2 = await getResponse2.ReadAsJsonAsync<JsonElement>();
        body2.GetProperty("interestedCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task UnrsvpEvent_WithoutExistingRsvp_Returns204()
    {
        var ctx = await SetupAsync();
        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId, name: "No Rsvp Yet");
        var eventId = created.GetProperty("id").ReadLong();

        // Un-RSVP when no RSVP exists (idempotent)
        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}/rsvp",
            ctx.Owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnrsvpEvent_AsNonMember_Returns403()
    {
        var ctx = await SetupAsync();
        var outsider = await _helper.RegisterUserAsync();

        var created = await CreateEventAsync(ctx.Owner.AccessToken, ctx.ServerId);
        var eventId = created.GetProperty("id").ReadLong();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/servers/{ctx.ServerId}/events/{eventId}/rsvp",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
