using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class PollTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public PollTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Helper Methods ────────────

    private async Task<(long ConversationId, string AccessToken)> SetupConversation()
    {
        var user = await _helper.RegisterUserAsync();
        var server = await _helper.CreateServerAsync(user.AccessToken);
        var serverId = server.GetProperty("id").ReadLong();
        var channel = await _helper.CreateChannelAsync(user.AccessToken, serverId);
        var conversationId = channel.GetProperty("conversationId").ReadLong();
        return (conversationId, user.AccessToken);
    }

    // ──────────── Create Poll ────────────

    [Fact]
    public async Task CreatePoll_WithOptions_Returns201()
    {
        var (conversationId, accessToken) = await SetupConversation();

        var response = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/polls",
            accessToken,
            new
            {
                question = "What's your favorite color?",
                options = new[]
                {
                    new { text = "Red", emojiUnicode = (string?)null },
                    new { text = "Blue", emojiUnicode = (string?)null },
                    new { text = "Green", emojiUnicode = (string?)null }
                },
                allowMultipleAnswers = false,
                expiresAt = (DateTimeOffset?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("pollId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("messageId").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("question").GetString().Should().Be("What's your favorite color?");

        var options = body.GetProperty("options").EnumerateArray().ToList();
        options.Should().HaveCount(3);
        options[0].GetProperty("text").GetString().Should().Be("Red");
        options[0].GetProperty("voteCount").GetInt32().Should().Be(0);
        options[0].GetProperty("position").GetInt32().Should().Be(0);
        options[1].GetProperty("text").GetString().Should().Be("Blue");
        options[1].GetProperty("voteCount").GetInt32().Should().Be(0);
        options[1].GetProperty("position").GetInt32().Should().Be(1);
        options[2].GetProperty("text").GetString().Should().Be("Green");
        options[2].GetProperty("voteCount").GetInt32().Should().Be(0);
        options[2].GetProperty("position").GetInt32().Should().Be(2);
    }

    // ──────────── Get Poll ────────────

    [Fact]
    public async Task GetPoll_Exists_Returns200()
    {
        var (conversationId, accessToken) = await SetupConversation();

        // Create poll
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/polls",
            accessToken,
            new
            {
                question = "Best programming language?",
                options = new[]
                {
                    new { text = "C#", emojiUnicode = (string?)null },
                    new { text = "TypeScript", emojiUnicode = (string?)null }
                },
                allowMultipleAnswers = false,
                expiresAt = (DateTimeOffset?)null
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var pollId = createBody.GetProperty("pollId").ReadLong();

        // Get poll
        var response = await _helper.AuthGetAsync(
            $"/api/v1/polls/{pollId}",
            accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().Be(pollId);
        body.GetProperty("question").GetString().Should().Be("Best programming language?");
        body.GetProperty("options").EnumerateArray().Should().HaveCount(2);
        body.GetProperty("isClosed").GetBoolean().Should().BeFalse();
    }

    // ──────────── Vote on Poll ────────────

    [Fact]
    public async Task VoteOnPoll_SingleAnswer_UpdatesVoteCount()
    {
        var (conversationId, accessToken) = await SetupConversation();

        // Create poll
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/polls",
            accessToken,
            new
            {
                question = "Yes or No?",
                options = new[]
                {
                    new { text = "Yes", emojiUnicode = (string?)null },
                    new { text = "No", emojiUnicode = (string?)null }
                },
                allowMultipleAnswers = false,
                expiresAt = (DateTimeOffset?)null
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var pollId = createBody.GetProperty("pollId").ReadLong();
        var options = createBody.GetProperty("options").EnumerateArray().ToList();
        var firstOptionId = options[0].GetProperty("id").ReadLong();

        // Vote for first option
        var request = TestHelper.AuthRequest(HttpMethod.Put, $"/api/v1/polls/{pollId}/vote", accessToken);
        request.Content = JsonContent.Create(new { optionIds = new[] { firstOptionId } }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("pollId").ReadLong().Should().Be(pollId);

        var updatedOptions = body.GetProperty("options").EnumerateArray().ToList();
        updatedOptions[0].GetProperty("id").ReadLong().Should().Be(firstOptionId);
        updatedOptions[0].GetProperty("voteCount").GetInt32().Should().Be(1);
        updatedOptions[1].GetProperty("voteCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task VoteOnPoll_MultipleAnswers_WhenAllowed_Succeeds()
    {
        var (conversationId, accessToken) = await SetupConversation();

        // Create poll with multiple answers allowed
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/polls",
            accessToken,
            new
            {
                question = "Select your favorite fruits",
                options = new[]
                {
                    new { text = "Apple", emojiUnicode = (string?)null },
                    new { text = "Banana", emojiUnicode = (string?)null },
                    new { text = "Orange", emojiUnicode = (string?)null }
                },
                allowMultipleAnswers = true,
                expiresAt = (DateTimeOffset?)null
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var pollId = createBody.GetProperty("pollId").ReadLong();
        var options = createBody.GetProperty("options").EnumerateArray().ToList();
        var firstOptionId = options[0].GetProperty("id").ReadLong();
        var thirdOptionId = options[2].GetProperty("id").ReadLong();

        // Vote for first and third options
        var request = TestHelper.AuthRequest(HttpMethod.Put, $"/api/v1/polls/{pollId}/vote", accessToken);
        request.Content = JsonContent.Create(new { optionIds = new[] { firstOptionId, thirdOptionId } }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var updatedOptions = body.GetProperty("options").EnumerateArray().ToList();
        updatedOptions[0].GetProperty("voteCount").GetInt32().Should().Be(1);
        updatedOptions[1].GetProperty("voteCount").GetInt32().Should().Be(0);
        updatedOptions[2].GetProperty("voteCount").GetInt32().Should().Be(1);
    }

    // ──────────── Retract Vote ────────────

    [Fact]
    public async Task RetractVote_AfterVoting_ResetsVoteCount()
    {
        var (conversationId, accessToken) = await SetupConversation();

        // Create poll
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/polls",
            accessToken,
            new
            {
                question = "Agree or Disagree?",
                options = new[]
                {
                    new { text = "Agree", emojiUnicode = (string?)null },
                    new { text = "Disagree", emojiUnicode = (string?)null }
                },
                allowMultipleAnswers = false,
                expiresAt = (DateTimeOffset?)null
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var pollId = createBody.GetProperty("pollId").ReadLong();
        var options = createBody.GetProperty("options").EnumerateArray().ToList();
        var firstOptionId = options[0].GetProperty("id").ReadLong();

        // Vote
        var voteRequest = TestHelper.AuthRequest(HttpMethod.Put, $"/api/v1/polls/{pollId}/vote", accessToken);
        voteRequest.Content = JsonContent.Create(new { optionIds = new[] { firstOptionId } }, options: JsonOptions);
        var voteResponse = await _fixture.Client.SendAsync(voteRequest);
        voteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Retract vote
        var retractResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/polls/{pollId}/vote",
            accessToken);

        retractResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify vote count is reset
        var getResponse = await _helper.AuthGetAsync(
            $"/api/v1/polls/{pollId}",
            accessToken);

        var getBody = await getResponse.ReadAsJsonAsync<JsonElement>();
        var updatedOptions = getBody.GetProperty("options").EnumerateArray().ToList();
        updatedOptions[0].GetProperty("voteCount").GetInt32().Should().Be(0);
        updatedOptions[1].GetProperty("voteCount").GetInt32().Should().Be(0);
    }

    // ──────────── End Poll ────────────

    [Fact]
    public async Task EndPoll_AsCreator_ClosesPoll()
    {
        var (conversationId, accessToken) = await SetupConversation();

        // Create poll
        var createResponse = await _helper.AuthPostAsync(
            $"/api/v1/conversations/{conversationId}/polls",
            accessToken,
            new
            {
                question = "Should we end this poll?",
                options = new[]
                {
                    new { text = "Yes", emojiUnicode = (string?)null },
                    new { text = "No", emojiUnicode = (string?)null }
                },
                allowMultipleAnswers = false,
                expiresAt = (DateTimeOffset?)null
            });

        var createBody = await createResponse.ReadAsJsonAsync<JsonElement>();
        var pollId = createBody.GetProperty("pollId").ReadLong();

        // End poll
        var response = await _helper.AuthPostAsync(
            $"/api/v1/polls/{pollId}/end",
            accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("pollId").ReadLong().Should().Be(pollId);
        body.GetProperty("isClosed").GetBoolean().Should().BeTrue();
    }
}
