using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Proves the instance-wide voice concurrency cap (Tier:MaxVoiceConcurrency,
/// set to 2 in WebAppFixture) holds under simultaneous joins. A check-then-act
/// race would admit more participants than the tier allows.
/// </summary>
[Collection("WebApp")]
public class VoiceConcurrencyTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public VoiceConcurrencyTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    [Fact]
    public async Task JoinVoiceChannel_ParallelJoinsBeyondTierCap_AdmitsExactlyCap()
    {
        // Voice states from other tests would consume the shared cap.
        await using (var cleanupDb = _fixture.CreateDbContext())
        {
            await cleanupDb.VoiceStates.ExecuteDeleteAsync();
        }

        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var voiceChannel = await _helper.CreateChannelAsync(owner.AccessToken, serverId, name: "voice-cap", type: 1);
        var voiceChannelId = voiceChannel.GetProperty("id").ReadLong();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);

        var users = new List<AuthenticatedUser> { owner };
        for (var i = 0; i < 4; i++)
        {
            var member = await _helper.RegisterUserAsync();
            (await _helper.JoinServerAsync(member.AccessToken, inviteCode)).EnsureSuccessStatusCode();
            users.Add(member);
        }

        var connections = users.Select(u => CreateHubConnection(u.AccessToken)).ToList();
        try
        {
            await Task.WhenAll(connections.Select(c => c.StartAsync().WaitAsync(TimeSpan.FromSeconds(5))));

            var joinResults = await Task.WhenAll(connections.Select(async c =>
            {
                try
                {
                    await c.InvokeAsync<JsonElement>("JoinVoiceChannel", voiceChannelId)
                        .WaitAsync(TimeSpan.FromSeconds(10));
                    return true;
                }
                catch (Microsoft.AspNetCore.SignalR.HubException)
                {
                    return false;
                }
            }));

            joinResults.Count(ok => ok).Should().Be(2,
                "the tier cap of 2 must admit exactly two of the simultaneous joiners");

            await using var db = _fixture.CreateDbContext();
            var voiceStates = await db.VoiceStates.AsNoTracking().CountAsync();
            voiceStates.Should().Be(2, "the database must never hold more voice participants than the cap");
        }
        finally
        {
            foreach (var connection in connections)
                await connection.DisposeAsync();
        }
    }

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
}
