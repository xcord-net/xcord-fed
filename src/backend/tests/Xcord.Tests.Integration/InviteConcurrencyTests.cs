using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Proves invite consumption and the denormalized Server.MemberCount stay
/// correct under concurrent joins/leaves (read-modify-write races would let
/// extra users through a capped invite and permanently drift the counter).
/// </summary>
[Collection("WebApp")]
public class InviteConcurrencyTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    public InviteConcurrencyTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    private async Task<string> CreateInviteWithMaxUsesAsync(string accessToken, long serverId, int maxUses)
    {
        var request = TestHelper.AuthRequest(HttpMethod.Post, $"/api/v1/servers/{serverId}/invites", accessToken);
        request.Content = JsonContent.Create(new { maxUses });
        var response = await _fixture.Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"invite creation should succeed: {await response.Content.ReadAsStringAsync()}");
        var body = await response.ReadAsJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }

    private async Task<(int MemberCount, int ActualMembers)> GetServerCountsAsync(string accessToken, long serverId)
    {
        var serverRequest = TestHelper.AuthRequest(HttpMethod.Get, $"/api/v1/servers/{serverId}", accessToken);
        var serverResponse = await _fixture.Client.SendAsync(serverRequest);
        serverResponse.EnsureSuccessStatusCode();
        var server = await serverResponse.ReadAsJsonAsync<JsonElement>();

        var membersRequest = TestHelper.AuthRequest(HttpMethod.Get, $"/api/v1/servers/{serverId}/members", accessToken);
        var membersResponse = await _fixture.Client.SendAsync(membersRequest);
        membersResponse.EnsureSuccessStatusCode();
        var members = await membersResponse.ReadAsJsonAsync<JsonElement>();
        var actualMembers = members.ValueKind == JsonValueKind.Array
            ? members.GetArrayLength()
            : members.GetProperty("members").GetArrayLength();

        return (server.GetProperty("memberCount").GetInt32(), actualMembers);
    }

    [Fact]
    public async Task JoinByInvite_ParallelJoinsOnSingleUseInvite_ExactlyOneSucceeds()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var inviteCode = await CreateInviteWithMaxUsesAsync(owner.AccessToken, serverId, maxUses: 1);

        var joiners = new List<AuthenticatedUser>();
        for (var i = 0; i < 4; i++)
            joiners.Add(await _helper.RegisterUserAsync());

        var responses = await Task.WhenAll(
            joiners.Select(j => _helper.JoinServerAsync(j.AccessToken, inviteCode)));

        responses.Count(r => r.IsSuccessStatusCode).Should().Be(1,
            "a single-use invite must admit exactly one of the concurrent joiners");
        responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest).Should().Be(3);

        var (memberCount, actualMembers) = await GetServerCountsAsync(owner.AccessToken, serverId);
        actualMembers.Should().Be(2, "owner plus the single admitted joiner");
        memberCount.Should().Be(actualMembers, "denormalized MemberCount must match the real membership");
    }

    [Fact]
    public async Task MemberCount_AfterConcurrentJoinsThenLeaves_MatchesActualMembership()
    {
        var owner = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(owner.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var inviteCode = await _helper.CreateInviteAsync(owner.AccessToken, serverId);

        var joiners = new List<AuthenticatedUser>();
        for (var i = 0; i < 8; i++)
            joiners.Add(await _helper.RegisterUserAsync());

        var joinResponses = await Task.WhenAll(
            joiners.Select(j => _helper.JoinServerAsync(j.AccessToken, inviteCode)));
        joinResponses.Should().OnlyContain(r => r.IsSuccessStatusCode);

        var afterJoin = await GetServerCountsAsync(owner.AccessToken, serverId);
        afterJoin.ActualMembers.Should().Be(9);
        afterJoin.MemberCount.Should().Be(9, "concurrent joins must each be counted exactly once");

        var leaveResponses = await Task.WhenAll(joiners.Select(j =>
        {
            var request = TestHelper.AuthRequest(HttpMethod.Delete, $"/api/v1/servers/{serverId}/members/@me", j.AccessToken);
            return _fixture.Client.SendAsync(request);
        }));
        leaveResponses.Should().OnlyContain(r => r.IsSuccessStatusCode);

        var afterLeave = await GetServerCountsAsync(owner.AccessToken, serverId);
        afterLeave.ActualMembers.Should().Be(1, "everyone but the owner left");
        afterLeave.MemberCount.Should().Be(1, "concurrent leaves must each be counted exactly once");
    }
}
