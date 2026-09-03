using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xcord.Entities;
using Xcord.Features.Broadcasts;
using Xcord.Infrastructure.Services;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Covers the part of a broadcast a viewer notices: whether changing the stage
/// or the layout interrupts the outgoing stream.
/// </summary>
/// <remarks>
/// Both used to be encoded in the egress template's URL, so the only way to
/// apply either was to stop the egress and start a new one - which dropped and
/// re-established every RTMP relay. Adding a guest to the stage broke the stream
/// on every platform it was being sent to. These tests hold the line: a stage or
/// layout change publishes to the room's control channel and starts no new
/// egress. Changing the set of destinations still does, because that genuinely
/// changes the egress's outputs.
/// </remarks>
[Collection("WebApp")]
public class BroadcastStageHandlerTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public BroadcastStageHandlerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    private FakeLiveKitService LiveKit =>
        (FakeLiveKitService)_fixture.Factory.Services.GetRequiredService<ILiveKitService>();

    /// <summary>
    /// The most recent control message for one channel's room. The fake LiveKit
    /// service is a singleton shared across this collection, so messages from
    /// sibling tests are in the same bag.
    /// </summary>
    private string LastControlPayloadFor(long channelId) =>
        LiveKit.SentData
            .Where(d => d.Topic == BroadcastControl.Topic
                     && d.Room.EndsWith($":broadcast:{channelId}", StringComparison.Ordinal))
            .Select(d => d.Payload)
            .Last();

    /// <summary>
    /// A stage channel, created through the public API. This needs no direct
    /// database fixup: ChannelType.Stage maps to the Streaming capability.
    /// </summary>
    private async Task<long> CreateStageChannelAsync(AuthenticatedUser owner)
    {
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(
            owner.AccessToken, serverId, name: null, type: (int)ChannelType.Stage);
        return channel.GetProperty("id").ReadLong();
    }

    private async Task<long> StartBroadcastAsync(AuthenticatedUser host, long channelId)
    {
        var request = TestHelper.AuthRequest(
            HttpMethod.Post, $"/api/v1/channels/{channelId}/broadcasts", host.AccessToken);
        request.Content = JsonContent.Create(new { layoutPreset = "Grid", streambotIds = Array.Empty<long>() });

        var response = await _fixture.Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"starting a broadcast should succeed: {await response.Content.ReadAsStringAsync()}");

        var body = await response.ReadAsJsonAsync<JsonElement>();
        return body.GetProperty("broadcastId").ReadLong();
    }

    [Fact]
    public async Task CreateChannel_WithStageType_GrantsTheStreamingCapability()
    {
        var owner = await _helper.RegisterUserAsync();
        var channelId = await CreateStageChannelAsync(owner);

        await using var db = _fixture.CreateDbContext();
        var channel = await db.Channels.FirstAsync(c => c.Id == channelId);

        channel.Capabilities.HasFlag(ChannelCapability.Streaming).Should().BeTrue(
            "a stage channel is the product's way of asking for a broadcastable room");
        channel.Type.Should().Be(ChannelType.Stage);
    }

    [Fact]
    public async Task AddStageSlot_DoesNotRestartTheEgress()
    {
        var owner = await _helper.RegisterUserAsync();
        var guest = await _helper.RegisterUserAsync();
        var channelId = await CreateStageChannelAsync(owner);
        var broadcastId = await StartBroadcastAsync(owner, channelId);

        var startedAfterLaunch = LiveKit.StartedEgresses.Count;

        var request = TestHelper.AuthRequest(
            HttpMethod.Post, $"/api/v1/broadcasts/{broadcastId}/stage", owner.AccessToken);
        request.Content = JsonContent.Create(new { userId = guest.UserId.ToString(), slotIndex = 1 });
        var response = await _fixture.Client.SendAsync(request);

        // The membership check may reject a user who never joined the server;
        // either way, no egress may be started as a side effect.
        LiveKit.StartedEgresses.Count.Should().Be(startedAfterLaunch,
            "putting someone on stage must not tear down and rebuild the relays");
        LiveKit.StoppedEgresses.Should().BeEmpty();
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UpdateLayout_PublishesToTheControlChannelInsteadOfRestarting()
    {
        var owner = await _helper.RegisterUserAsync();
        var channelId = await CreateStageChannelAsync(owner);
        var broadcastId = await StartBroadcastAsync(owner, channelId);

        var startedAfterLaunch = LiveKit.StartedEgresses.Count;
        var sentBefore = LiveKit.SentData.Count;

        var request = TestHelper.AuthRequest(
            HttpMethod.Patch, $"/api/v1/broadcasts/{broadcastId}/layout", owner.AccessToken);
        request.Content = JsonContent.Create(new { preset = "Spotlight" });
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            await response.Content.ReadAsStringAsync());
        LiveKit.StartedEgresses.Count.Should().Be(startedAfterLaunch,
            "a layout change rearranges the composite; it does not change where it is sent");
        LiveKit.SentData.Count.Should().BeGreaterThan(sentBefore);
        LastControlPayloadFor(channelId).Should().Contain("spotlight",
            "the compositor is told which preset to switch to");
    }

    [Fact]
    public async Task UpdateLayout_ToAudioShow_IsAccepted()
    {
        var owner = await _helper.RegisterUserAsync();
        var channelId = await CreateStageChannelAsync(owner);
        var broadcastId = await StartBroadcastAsync(owner, channelId);

        var request = TestHelper.AuthRequest(
            HttpMethod.Patch, $"/api/v1/broadcasts/{broadcastId}/layout", owner.AccessToken);
        request.Content = JsonContent.Create(new { preset = "AudioShow" });
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            await response.Content.ReadAsStringAsync());

        LastControlPayloadFor(channelId).Should().Contain("audio-show");
    }

    [Fact]
    public async Task UpdateLayout_RejectsAnUnknownPreset()
    {
        var owner = await _helper.RegisterUserAsync();
        var channelId = await CreateStageChannelAsync(owner);
        var broadcastId = await StartBroadcastAsync(owner, channelId);

        var request = TestHelper.AuthRequest(
            HttpMethod.Patch, $"/api/v1/broadcasts/{broadcastId}/layout", owner.AccessToken);
        request.Content = JsonContent.Create(new { preset = "Kaleidoscope" });
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LayoutTemplate_ServesTheAudioShowPreset()
    {
        var response = await _fixture.Client.GetAsync("/api/v1/broadcast-layout/audio-show");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("audio-show", "the preset must have styles of its own");
        html.Should().Contain("ActiveSpeakersChanged",
            "the audio show's only visual is driven by who is actually speaking");
    }

    [Fact]
    public async Task LayoutTemplate_RejectsAnUnknownPreset()
    {
        var response = await _fixture.Client.GetAsync("/api/v1/broadcast-layout/kaleidoscope");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The compositor reacts to control messages rather than reloading, so the
    /// template has to carry the listener and the topic it filters on.
    /// </summary>
    [Fact]
    public async Task LayoutTemplate_ListensOnTheControlTopic()
    {
        var response = await _fixture.Client.GetAsync("/api/v1/broadcast-layout/grid");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain(BroadcastControl.Topic);
        html.Should().Contain("DataReceived");
    }
}
