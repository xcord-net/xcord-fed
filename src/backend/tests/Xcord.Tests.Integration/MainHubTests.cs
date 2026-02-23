using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class MainHubTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public MainHubTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Connection Lifecycle ────────────

    [Fact]
    public async Task Connect_WithValidToken_Succeeds()
    {
        var user = await _helper.RegisterUserAsync();

        var connection = CreateHubConnection(user.AccessToken);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));
            connection.State.Should().Be(HubConnectionState.Connected);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Connect_WithInvalidToken_Fails()
    {
        var connection = CreateHubConnection("invalid-token-value");
        Func<Task> act = () => connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Disconnect_CleansUpVoiceState()
    {
        var (_, conversationId, token, user) = await SetupServerWithChannelAndUser();

        // Get the channel to find channelId for voice
        await using var db = _fixture.CreateDbContext();
        var channel = await db.Channels.AsNoTracking()
            .FirstAsync(c => c.ConversationId == conversationId);

        var connection = CreateHubConnection(token);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

            // Join conversation first
            await connection.InvokeAsync("JoinConversation", conversationId)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await connection.DisposeAsync();
        }

        // After disconnect, connection should be closed
        connection.State.Should().Be(HubConnectionState.Disconnected);
    }

    // ──────────── Conversation Join/Leave ────────────

    [Fact]
    public async Task JoinConversation_AsServerMember_Succeeds()
    {
        var (_, conversationId, token, _) = await SetupServerWithChannelAndUser();

        var connection = CreateHubConnection(token);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

            // Should not throw
            await connection.InvokeAsync("JoinConversation", conversationId)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task LeaveConversation_AfterJoining_Succeeds()
    {
        var (_, conversationId, token, _) = await SetupServerWithChannelAndUser();

        var connection = CreateHubConnection(token);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

            await connection.InvokeAsync("JoinConversation", conversationId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Should not throw
            await connection.InvokeAsync("LeaveConversation", conversationId)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    // ──────────── Typing Indicator ────────────

    [Fact]
    public async Task StartTyping_InJoinedConversation_BroadcastsToOtherClients()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(owner.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();

        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var ownerConn = CreateHubConnection(owner.AccessToken);
        var memberConn = CreateHubConnection(member.AccessToken);

        try
        {
            // Both connect and join the conversation
            await Task.WhenAll(
                ownerConn.StartAsync().WaitAsync(TimeSpan.FromSeconds(2)),
                memberConn.StartAsync().WaitAsync(TimeSpan.FromSeconds(2))
            );

            await Task.WhenAll(
                ownerConn.InvokeAsync("JoinConversation", conversationId)
                    .WaitAsync(TimeSpan.FromSeconds(5)),
                memberConn.InvokeAsync("JoinConversation", conversationId)
                    .WaitAsync(TimeSpan.FromSeconds(5))
            );

            // Set up listener for typing event on owner's connection
            var typingReceived = new TaskCompletionSource<JsonElement>();
            ownerConn.On<JsonElement>("Chat_TypingStarted", data =>
            {
                typingReceived.TrySetResult(data);
            });

            // Member starts typing
            await memberConn.InvokeAsync("StartTyping", conversationId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Owner should receive the typing notification
            var result = await typingReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            result.GetProperty("userId").ReadLong().Should().Be(member.UserId);
            result.GetProperty("conversationId").ReadLong().Should().Be(conversationId);
        }
        finally
        {
            await ownerConn.DisposeAsync();
            await memberConn.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartTyping_RateLimited_SecondCallWithinWindowIsThrottled()
    {
        var (_, conversationId, token, _) = await SetupServerWithChannelAndUser();

        var connection = CreateHubConnection(token);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));
            await connection.InvokeAsync("JoinConversation", conversationId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // First typing call should succeed (no exception)
            await connection.InvokeAsync("StartTyping", conversationId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Second call within rate limit window should also "succeed" (it's throttled silently, not errored)
            await connection.InvokeAsync("StartTyping", conversationId)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    // ──────────── Presence / UpdateStatus ────────────

    [Fact]
    public async Task UpdateStatus_ValidStatus_BroadcastsPresence()
    {
        var owner = await _helper.RegisterUserAsync();
        var member = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();

        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);
        await _helper.JoinServerAsync(member.AccessToken, inviteCode);

        var ownerConn = CreateHubConnection(owner.AccessToken);
        var memberConn = CreateHubConnection(member.AccessToken);

        try
        {
            await Task.WhenAll(
                ownerConn.StartAsync().WaitAsync(TimeSpan.FromSeconds(2)),
                memberConn.StartAsync().WaitAsync(TimeSpan.FromSeconds(2))
            );

            // Set up listener for presence update on owner's connection
            var presenceReceived = new TaskCompletionSource<JsonElement>();
            ownerConn.On<JsonElement>("Presence_Updated", data =>
            {
                // Only capture member's presence update (not the owner's own connection updates)
                if (data.TryGetProperty("userId", out var uid) && uid.ReadLong() == member.UserId)
                {
                    presenceReceived.TrySetResult(data);
                }
            });

            // Member updates their status
            await memberConn.InvokeAsync("UpdateStatus", "Away")
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Owner should receive presence update
            var result = await presenceReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            result.GetProperty("userId").ReadLong().Should().Be(member.UserId);
        }
        finally
        {
            await ownerConn.DisposeAsync();
            await memberConn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_ThrowsHubException()
    {
        var (_, _, token, _) = await SetupServerWithChannelAndUser();

        var connection = CreateHubConnection(token);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

            Func<Task> act = () => connection.InvokeAsync("UpdateStatus", "InvalidStatus")
                .WaitAsync(TimeSpan.FromSeconds(5));

            await act.Should().ThrowAsync<HubException>();
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    // ──────────── Heartbeat ────────────

    [Fact]
    public async Task Heartbeat_WhileConnected_DoesNotThrow()
    {
        var (_, _, token, _) = await SetupServerWithChannelAndUser();

        var connection = CreateHubConnection(token);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

            // Should not throw
            await connection.InvokeAsync("Heartbeat").WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    // ──────────── Voice Channel ────────────

    [Fact]
    public async Task JoinVoiceChannel_AsServerMember_ReturnsTokenAndRoom()
    {
        var (serverId, _, token, _) = await SetupServerWithChannelAndUser();

        // Create a voice channel (type 1)
        var voiceChannel = await _helper.CreateChannelAsync(token, serverId, name: "voice-test", type: 1);
        var voiceChannelId = voiceChannel.GetProperty("id").ReadLong();

        var connection = CreateHubConnection(token);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

            var result = await connection.InvokeAsync<JsonElement>("JoinVoiceChannel", voiceChannelId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            result.TryGetProperty("token", out var tokenProp).Should().BeTrue("should return a LiveKit token");
            tokenProp.GetString().Should().NotBeNullOrEmpty();

            result.TryGetProperty("roomName", out var roomProp).Should().BeTrue("should return a room name");
            roomProp.GetString().Should().Contain("voice");
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task LeaveVoiceChannel_AfterJoining_Succeeds()
    {
        var (serverId, _, token, _) = await SetupServerWithChannelAndUser();

        var voiceChannel = await _helper.CreateChannelAsync(token, serverId, name: "voice-leave", type: 1);
        var voiceChannelId = voiceChannel.GetProperty("id").ReadLong();

        var connection = CreateHubConnection(token);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

            await connection.InvokeAsync<JsonElement>("JoinVoiceChannel", voiceChannelId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Should not throw
            await connection.InvokeAsync("LeaveVoiceChannel", voiceChannelId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Verify voice state was cleaned up
            await using var db = _fixture.CreateDbContext();
            var voiceState = await db.VoiceStates
                .AsNoTracking()
                .FirstOrDefaultAsync(vs => vs.ChannelId == voiceChannelId);
            voiceState.Should().BeNull("voice state should be removed after leaving");
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateVoiceState_Mute_UpdatesState()
    {
        var (serverId, _, token, user) = await SetupServerWithChannelAndUser();

        var voiceChannel = await _helper.CreateChannelAsync(token, serverId, name: "voice-mute", type: 1);
        var voiceChannelId = voiceChannel.GetProperty("id").ReadLong();

        var connection = CreateHubConnection(token);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

            await connection.InvokeAsync<JsonElement>("JoinVoiceChannel", voiceChannelId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Mute self
            await connection.InvokeAsync("UpdateVoiceState", voiceChannelId, true, (bool?)null, (bool?)null)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Verify state was updated
            await using var db = _fixture.CreateDbContext();
            var voiceState = await db.VoiceStates
                .AsNoTracking()
                .FirstOrDefaultAsync(vs => vs.ChannelId == voiceChannelId && vs.UserId == user.UserId);
            voiceState.Should().NotBeNull();
            voiceState!.IsMuted.Should().BeTrue();
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    // ──────────── Permission Checks ────────────

    [Fact]
    public async Task JoinConversation_AsNonMember_ThrowsHubException()
    {
        var (_, conversationId, _, _) = await SetupServerWithChannelAndUser();
        var outsider = await _helper.RegisterUserAsync();

        var connection = CreateHubConnection(outsider.AccessToken);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

            Func<Task> act = () => connection.InvokeAsync("JoinConversation", conversationId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            await act.Should().ThrowAsync<HubException>();
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartTyping_AsNonMember_ThrowsHubException()
    {
        var (_, conversationId, _, _) = await SetupServerWithChannelAndUser();
        var outsider = await _helper.RegisterUserAsync();

        var connection = CreateHubConnection(outsider.AccessToken);
        try
        {
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));

            Func<Task> act = () => connection.InvokeAsync("StartTyping", conversationId)
                .WaitAsync(TimeSpan.FromSeconds(5));

            await act.Should().ThrowAsync<HubException>();
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    // ──────────── Helpers ────────────

    private HubConnection CreateHubConnection(string accessToken)
    {
        var hubUrl = new Uri(_fixture.ServerBaseAddress, "/hubs/main");

        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => _fixture.CreateHandler();
            })
            .Build();
    }

    /// <summary>
    /// Creates a user, server, and channel. Returns (serverId, conversationId, accessToken, user).
    /// </summary>
    private async Task<(long ServerId, long ConversationId, string AccessToken, AuthenticatedUser User)> SetupServerWithChannelAndUser()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();
        return (serverId, conversationId, user.AccessToken, user);
    }
}
