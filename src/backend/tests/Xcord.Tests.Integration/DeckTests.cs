using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// The Deck shell has no sidebar to fill in lazily, so /api/v1/deck has to
/// return the user's whole map in one call. These cover the parts that would
/// silently break the shell: missing communities, leaked ones, and unread
/// totals that disagree with the badge.
/// </summary>
[Collection("WebApp")]
public class DeckTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public DeckTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    private async Task<JsonElement> GetDeckAsync(string accessToken)
    {
        var response = await _helper.AuthGetAsync("/api/v1/deck", accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task GetDeck_RequiresAuthentication()
    {
        var response = await _fixture.Client.GetAsync("/api/v1/deck");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDeck_ForNewUser_ReturnsEmptyMapRatherThanFailing()
    {
        var user = await _helper.RegisterUserAsync();

        var deck = await GetDeckAsync(user.AccessToken);

        deck.GetProperty("communities").GetArrayLength().Should().Be(0);
        deck.GetProperty("totalUnread").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetDeck_IncludesCommunitiesTheUserBelongsTo()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken, "The Foundry");
        var serverId = server.GetProperty("id").GetString();

        var deck = await GetDeckAsync(user.AccessToken);

        var communities = deck.GetProperty("communities").EnumerateArray().ToList();
        communities.Should().ContainSingle();
        communities[0].GetProperty("id").GetString().Should().Be(serverId);
        communities[0].GetProperty("name").GetString().Should().Be("The Foundry");
    }

    [Fact]
    public async Task GetDeck_IncludesChannelsWithTheirConversationIds()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = long.Parse(server.GetProperty("id").GetString()!);
        await _helper.CreateChannelAsync(user.AccessToken, serverId, "forge-floor");

        var deck = await GetDeckAsync(user.AccessToken);

        var conversations = deck.GetProperty("communities")[0]
            .GetProperty("conversations").EnumerateArray().ToList();

        conversations.Should().NotBeEmpty();
        var channel = conversations.FirstOrDefault(c =>
            c.GetProperty("name").GetString() == "forge-floor");
        channel.ValueKind.Should().NotBe(JsonValueKind.Undefined);

        // The client keys unread off the conversation, so a zero here would
        // silently break every badge on that tab.
        channel.GetProperty("conversationId").GetString().Should().NotBeNullOrEmpty();
        channel.GetProperty("conversationId").GetString().Should().NotBe("0");
    }

    [Fact]
    public async Task GetDeck_DoesNotLeakCommunitiesTheUserIsNotIn()
    {
        var owner = await _helper.RegisterUserAsync();
        await _helper.CreateServerAsync(owner.AccessToken, "Private Foundry");

        var stranger = await _helper.RegisterUserAsync();
        var deck = await GetDeckAsync(stranger.AccessToken);

        deck.GetProperty("communities").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetDeck_ReportsZeroUnreadForAFreshChannel()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = long.Parse(server.GetProperty("id").GetString()!);
        await _helper.CreateChannelAsync(user.AccessToken, serverId, "quiet-room");

        var deck = await GetDeckAsync(user.AccessToken);

        var conversations = deck.GetProperty("communities")[0]
            .GetProperty("conversations").EnumerateArray();
        foreach (var c in conversations)
        {
            c.GetProperty("unread").GetInt32().Should().Be(0);
            c.GetProperty("mentions").GetInt32().Should().Be(0);
        }
    }

    [Fact]
    public async Task GetDeck_ReportsNoLiveVoiceWhenNobodyIsConnected()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = long.Parse(server.GetProperty("id").GetString()!);
        await _helper.CreateChannelAsync(user.AccessToken, serverId, "empty-voice");

        var deck = await GetDeckAsync(user.AccessToken);

        foreach (var c in deck.GetProperty("communities")[0].GetProperty("conversations").EnumerateArray())
        {
            c.GetProperty("liveVoiceCount").GetInt32().Should().Be(0);
        }
    }

    [Fact]
    public async Task GetDeck_AlwaysReturnsADirectMessagesCollection()
    {
        var user = await _helper.RegisterUserAsync();

        var deck = await GetDeckAsync(user.AccessToken);

        // The switchboard groups by this unconditionally; a missing property
        // would throw client-side rather than render an empty group.
        deck.TryGetProperty("directMessages", out var dms).Should().BeTrue();
        dms.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
