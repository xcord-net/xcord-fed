using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class CallTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public CallTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helper ────────────

    /// <summary>
    /// Creates a 1:1 DM between two users and returns the DM channel ID.
    /// </summary>
    private async Task<long> CreateOneOnOneDmAsync(AuthenticatedUser user1, AuthenticatedUser user2)
    {
        var response = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            user1.AccessToken,
            new { recipientIds = new[] { user2.UserId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dm = await response.ReadAsJsonAsync<JsonElement>();
        dm.GetProperty("isGroup").GetBoolean().Should().BeFalse();
        return dm.GetProperty("id").ReadLong();
    }

    /// <summary>
    /// Initiates a call on a DM channel and returns the call ID.
    /// </summary>
    private async Task<long> InitiateCallAsync(string accessToken, long dmChannelId)
    {
        var response = await _helper.AuthPostAsync(
            $"/api/v1/users/@me/dms/{dmChannelId}/call",
            accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        return body.GetProperty("callId").ReadLong();
    }

    // ──────────── Initiate Call ────────────

    [Fact]
    public async Task InitiateCall_OneOnOneDm_Returns200WithCallId()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/users/@me/dms/{dmChannelId}/call",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("callId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("dmChannelId").ReadLong().Should().Be(dmChannelId);
    }

    [Fact]
    public async Task InitiateCall_NonExistentDmChannel_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms/999999999999/call",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InitiateCall_DuplicateCall_Returns409()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);

        // First call should succeed
        var response1 = await _helper.AuthPostAsync(
            $"/api/v1/users/@me/dms/{dmChannelId}/call",
            caller.AccessToken);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second call while first is still ringing should return 409
        var response2 = await _helper.AuthPostAsync(
            $"/api/v1/users/@me/dms/{dmChannelId}/call",
            caller.AccessToken);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InitiateCall_GroupDm_Returns400()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();
        var user3 = await _helper.RegisterUserAsync();

        // Create a group DM (3 members)
        var response = await _helper.AuthPostAsync(
            "/api/v1/users/@me/dms",
            user1.AccessToken,
            new { recipientIds = new[] { user2.UserId, user3.UserId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dm = await response.ReadAsJsonAsync<JsonElement>();
        var dmChannelId = dm.GetProperty("id").ReadLong();
        dm.GetProperty("isGroup").GetBoolean().Should().BeTrue();

        // Attempt to initiate call on group DM
        var callResponse = await _helper.AuthPostAsync(
            $"/api/v1/users/@me/dms/{dmChannelId}/call",
            user1.AccessToken);

        callResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InitiateCall_NotDmMember_Returns403()
    {
        var user1 = await _helper.RegisterUserAsync();
        var user2 = await _helper.RegisterUserAsync();
        var outsider = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(user1, user2);

        // Outsider tries to initiate a call in a DM they aren't part of
        var response = await _helper.AuthPostAsync(
            $"/api/v1/users/@me/dms/{dmChannelId}/call",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Answer Call ────────────

    [Fact]
    public async Task AnswerCall_PendingCall_Returns200WithToken()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/answer",
            recipient.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("roomName").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnswerCall_NonExistentCall_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/calls/999999999999/answer",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnswerCall_CallerTriesToAnswer_Returns400()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        // Caller tries to answer their own call
        var response = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/answer",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnswerCall_AlreadyAnswered_Returns400()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        // Answer the call
        var response1 = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/answer",
            recipient.AccessToken);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Try to answer again
        var response2 = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/answer",
            recipient.AccessToken);
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Decline Call ────────────

    [Fact]
    public async Task DeclineCall_PendingCall_Returns200()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        var response = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/decline",
            recipient.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeclineCall_NonExistentCall_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/calls/999999999999/decline",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeclineCall_CallerTriesToDecline_Returns400()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        // Caller tries to decline their own call
        var response = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/decline",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeclineCall_AlreadyDeclined_Returns400()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        // Decline the call
        var response1 = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/decline",
            recipient.AccessToken);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Try to decline again — call is no longer Ringing
        var response2 = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/decline",
            recipient.AccessToken);
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── End Call ────────────

    [Fact]
    public async Task EndCall_ActiveCall_Returns200()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        // Answer the call first
        var answerResponse = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/answer",
            recipient.AccessToken);
        answerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // End the active call
        var response = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/end",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EndCall_RingingCall_Returns200()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        // End the ringing call (caller hangs up before recipient answers)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/end",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EndCall_RecipientEndsActiveCall_Returns200()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        // Answer the call
        var answerResponse = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/answer",
            recipient.AccessToken);
        answerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Recipient ends the call
        var response = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/end",
            recipient.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EndCall_NonExistentCall_Returns404()
    {
        var user = await _helper.RegisterUserAsync();

        var response = await _helper.AuthPostAsync(
            "/api/v1/calls/999999999999/end",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EndCall_AlreadyEnded_Returns400()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        // End the call
        var response1 = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/end",
            caller.AccessToken);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Try to end again
        var response2 = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/end",
            caller.AccessToken);
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EndCall_NonParticipant_Returns403()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var outsider = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);
        var callId = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        // Outsider tries to end the call
        var response = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId}/end",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InitiateCall_AfterPreviousEnded_Succeeds()
    {
        var caller = await _helper.RegisterUserAsync();
        var recipient = await _helper.RegisterUserAsync();
        var dmChannelId = await CreateOneOnOneDmAsync(caller, recipient);

        // First call
        var callId1 = await InitiateCallAsync(caller.AccessToken, dmChannelId);

        // End the first call
        var endResponse = await _helper.AuthPostAsync(
            $"/api/v1/calls/{callId1}/end",
            caller.AccessToken);
        endResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second call should succeed (no active/ringing call anymore)
        var response = await _helper.AuthPostAsync(
            $"/api/v1/users/@me/dms/{dmChannelId}/call",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var callId2 = body.GetProperty("callId").ReadLong();
        callId2.Should().NotBe(callId1);
    }
}
