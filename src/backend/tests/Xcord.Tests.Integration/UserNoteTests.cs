using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class UserNoteTests
{
    private readonly WebAppFixture _fixture;
    private readonly TestHelper _helper;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public UserNoteTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _helper = new TestHelper(fixture);
    }

    // ──────────── Upsert (Create) User Note ────────────

    [Fact]
    public async Task UpsertUserNote_Create_Returns200()
    {
        var author = await _helper.RegisterUserAsync();
        var target = await _helper.RegisterUserAsync();

        var request = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);
        request.Content = JsonContent.Create(new { content = "This is a note about the user" }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("id").ReadLong().Should().BeGreaterThan(0);
        body.GetProperty("targetUserId").ReadLong().Should().Be(target.UserId);
        body.GetProperty("content").GetString().Should().Be("This is a note about the user");
        body.TryGetProperty("createdAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task UpsertUserNote_Update_OverwritesContent()
    {
        var author = await _helper.RegisterUserAsync();
        var target = await _helper.RegisterUserAsync();

        // Create initial note
        var createRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);
        createRequest.Content = JsonContent.Create(new { content = "Original note" }, options: JsonOptions);
        var createResponse = await _fixture.Client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await createResponse.ReadAsJsonAsync<JsonElement>();
        var noteId = created.GetProperty("id").ReadLong();

        // Update the note
        var updateRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);
        updateRequest.Content = JsonContent.Create(new { content = "Updated note content" }, options: JsonOptions);
        var updateResponse = await _fixture.Client.SendAsync(updateRequest);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResponse.ReadAsJsonAsync<JsonElement>();
        updated.GetProperty("id").ReadLong().Should().Be(noteId, "upsert should update the same note");
        updated.GetProperty("content").GetString().Should().Be("Updated note content");
        updated.TryGetProperty("updatedAt", out var updatedAt).Should().BeTrue();
        updatedAt.ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task UpsertUserNote_AboutSelf_Returns400()
    {
        var user = await _helper.RegisterUserAsync();

        var request = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/{user.UserId}/notes",
            user.AccessToken);
        request.Content = JsonContent.Create(new { content = "Self note" }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpsertUserNote_EmptyContent_Returns400()
    {
        var author = await _helper.RegisterUserAsync();
        var target = await _helper.RegisterUserAsync();

        var request = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);
        request.Content = JsonContent.Create(new { content = "" }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpsertUserNote_NonExistentTarget_Returns404()
    {
        var author = await _helper.RegisterUserAsync();

        var request = TestHelper.AuthRequest(
            HttpMethod.Put,
            "/api/v1/users/999999999999/notes",
            author.AccessToken);
        request.Content = JsonContent.Create(new { content = "Note for nobody" }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Get User Note ────────────

    [Fact]
    public async Task GetUserNote_Exists_Returns200()
    {
        var author = await _helper.RegisterUserAsync();
        var target = await _helper.RegisterUserAsync();

        // Create note
        var createRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);
        createRequest.Content = JsonContent.Create(new { content = "Retrievable note" }, options: JsonOptions);
        var createResponse = await _fixture.Client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Get note
        var getResponse = await _helper.AuthGetAsync(
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await getResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("targetUserId").ReadLong().Should().Be(target.UserId);
        body.GetProperty("content").GetString().Should().Be("Retrievable note");
    }

    [Fact]
    public async Task GetUserNote_NotExists_Returns404()
    {
        var author = await _helper.RegisterUserAsync();
        var target = await _helper.RegisterUserAsync();

        var response = await _helper.AuthGetAsync(
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserNote_OtherUserNote_Returns404()
    {
        var author = await _helper.RegisterUserAsync();
        var target = await _helper.RegisterUserAsync();
        var otherUser = await _helper.RegisterUserAsync();

        // Author creates a note about target
        var createRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);
        createRequest.Content = JsonContent.Create(new { content = "Private note" }, options: JsonOptions);
        var createResponse = await _fixture.Client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Other user tries to read author's note about target
        var getResponse = await _helper.AuthGetAsync(
            $"/api/v1/users/{target.UserId}/notes",
            otherUser.AccessToken);

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────── Delete User Note ────────────

    [Fact]
    public async Task DeleteUserNote_Exists_Returns200()
    {
        var author = await _helper.RegisterUserAsync();
        var target = await _helper.RegisterUserAsync();

        // Create note
        var createRequest = TestHelper.AuthRequest(
            HttpMethod.Put,
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);
        createRequest.Content = JsonContent.Create(new { content = "To be deleted" }, options: JsonOptions);
        var createResponse = await _fixture.Client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Delete note
        var deleteResponse = await _helper.AuthDeleteAsync(
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await deleteResponse.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("deleted").GetBoolean().Should().BeTrue();

        // Verify note is no longer retrievable
        var getResponse = await _helper.AuthGetAsync(
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUserNote_NotExists_Returns404()
    {
        var author = await _helper.RegisterUserAsync();
        var target = await _helper.RegisterUserAsync();

        var response = await _helper.AuthDeleteAsync(
            $"/api/v1/users/{target.UserId}/notes",
            author.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
