using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xcord.Entities;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

[Collection("WebApp")]
public class AuthFlowTests
{
    private readonly WebAppFixture _fixture;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public AuthFlowTests(WebAppFixture fixture)
    {
        _fixture = fixture;
    }

    // ──────────── Registration ────────────

    [Fact]
    public async Task Register_ValidInput_ReturnsUserWithAccessToken()
    {
        var (username, email) = UniqueCredentials();
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username,
            displayName = "Test User",
            email,
            password = "TestPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        TestHelper.ParseSnowflakeId(body.GetProperty("userId")).Should().BeGreaterThan(0);
        body.GetProperty("username").GetString().Should().Be(username);
        body.GetProperty("authenticated").GetBoolean().Should().BeTrue();
        ExtractAccessTokenCookie(response).Should().NotBeNullOrEmpty();
        body.GetProperty("emailConfirmed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Register_SetsRefreshTokenCookie()
    {
        var (username, email) = UniqueCredentials();
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username,
            displayName = "Test",
            email,
            password = "TestPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshToken = ExtractRefreshTokenCookie(response);
        refreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        var (username, email1) = UniqueCredentials();
        var email2 = $"alt_{Guid.NewGuid():N}@xcord.local";

        await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username,
            displayName = "First",
            email = email1,
            password = "TestPassword123!"
        });

        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username,
            displayName = "Second",
            email = email2,
            password = "TestPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var email = $"dup_{Guid.NewGuid():N}@xcord.local";

        await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username = $"user1_{Guid.NewGuid():N}"[..20],
            displayName = "First",
            email,
            password = "TestPassword123!"
        });

        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username = $"user2_{Guid.NewGuid():N}"[..20],
            displayName = "Second",
            email,
            password = "TestPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        var (username, email) = UniqueCredentials();
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username,
            displayName = "Test",
            email,
            password = "short"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Login ────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAccessToken()
    {
        var (_, email, password, _) = await RegisterUserAsync();

        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        TestHelper.ParseSnowflakeId(body.GetProperty("userId")).Should().BeGreaterThan(0);
        body.GetProperty("authenticated").GetBoolean().Should().BeTrue();
        ExtractAccessTokenCookie(response).Should().NotBeNullOrEmpty();
        ExtractRefreshTokenCookie(response).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsBadRequest()
    {
        var (_, email, _, _) = await RegisterUserAsync();

        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "WrongPassword999!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_NonexistentEmail_ReturnsBadRequest()
    {
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email = $"nonexistent_{Guid.NewGuid():N}@xcord.local",
            password = "TestPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_DisabledAccount_ReturnsForbidden()
    {
        var (userId, email, password, _) = await RegisterUserAsync();

        // Disable the account via DB
        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.FindAsync(userId);
        user!.IsDisabled = true;
        await db.SaveChangesAsync();

        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Email Confirmation ────────────

    [Fact]
    public async Task ConfirmEmail_ValidCode_SetsEmailConfirmed()
    {
        var (userId, _, _, accessToken) = await RegisterUserAsync();

        // Read the confirmation code from DB
        await using var db = _fixture.CreateDbContext();
        var token = await db.EmailConfirmationTokens
            .FirstOrDefaultAsync(t => t.UserId == userId);
        token.Should().NotBeNull();

        // Confirm email
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/confirm-email?code={token!.Code}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("confirmed").GetBoolean().Should().BeTrue();

        // Verify in DB
        await using var db2 = _fixture.CreateDbContext();
        var updatedUser = await db2.Users.FindAsync(userId);
        updatedUser!.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmEmail_InvalidCode_ReturnsBadRequest()
    {
        var (_, _, _, accessToken) = await RegisterUserAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/confirm-email?code=000000");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConfirmEmail_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _fixture.Client.PostAsync("/api/v1/auth/confirm-email?code=123456", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Refresh Token ────────────

    [Fact]
    public async Task RefreshToken_ValidCookie_RotatesAndReturnsNewAccessToken()
    {
        // Get the refresh token from the registration response
        var (_, email, password, _) = await RegisterUserAsync();
        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new { email, password });
        var refreshToken = ExtractRefreshTokenCookie(loginResponse);
        refreshToken.Should().NotBeNullOrEmpty();

        // Use the refresh token
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.ReadAsJsonAsync<JsonElement>();
        body.GetProperty("authenticated").GetBoolean().Should().BeTrue();
        ExtractAccessTokenCookie(response).Should().NotBeNullOrEmpty();

        // New refresh token cookie should be set
        var newRefreshToken = ExtractRefreshTokenCookie(response);
        newRefreshToken.Should().NotBeNullOrEmpty();
        newRefreshToken.Should().NotBe(refreshToken); // Token was rotated
    }

    [Fact]
    public async Task RefreshToken_AlreadyUsed_ReturnsBadRequest()
    {
        var (_, email, password, _) = await RegisterUserAsync();
        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new { email, password });
        var refreshToken = ExtractRefreshTokenCookie(loginResponse);

        // Use it once (succeeds, old token deleted)
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request1.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        var response1 = await _fixture.Client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Try to use the same token again (should fail — it was rotated)
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request2.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        var response2 = await _fixture.Client.SendAsync(request2);

        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_MissingCookie_ReturnsBadRequest()
    {
        var response = await _fixture.Client.PostAsync("/api/v1/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_InvalidValue_ReturnsBadRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", "refresh_token=totally-invalid-token");
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Email Confirmation Enforcement ────────────

    [Fact]
    public async Task ProtectedEndpoint_UnconfirmedEmail_ReturnsForbidden()
    {
        var (_, _, _, accessToken) = await RegisterUserAsync();

        // Try to create a server (protected endpoint requiring confirmed email)
        var request = AuthRequest(HttpMethod.Post, "/api/v1/servers", accessToken);
        request.Content = JsonContent.Create(new { name = "TestServer" });
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProtectedEndpoint_ConfirmedEmail_Succeeds()
    {
        var (userId, _, _, accessToken) = await RegisterConfirmedUserAsync();

        // Create a server (should succeed with confirmed email)
        var request = AuthRequest(HttpMethod.Post, "/api/v1/servers", accessToken);
        request.Content = JsonContent.Create(new { name = "TestServer" });
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ──────────── Change Password ────────────

    [Fact]
    public async Task ChangePassword_Valid_ChangesPasswordAndInvalidatesRefreshTokens()
    {
        var (userId, email, password, accessToken) = await RegisterConfirmedUserAsync();

        var request = AuthRequest(HttpMethod.Post, "/api/v1/auth/change-password", accessToken);
        request.Content = JsonContent.Create(new
        {
            currentPassword = password,
            newPassword = "NewPassword456!"
        }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // All refresh tokens should be deleted
        await using var db = _fixture.CreateDbContext();
        var tokenCount = await db.RefreshTokens.CountAsync(rt => rt.UserId == userId);
        tokenCount.Should().Be(0);

        // Login with new password should work
        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "NewPassword456!"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Login with old password should fail
        var oldLoginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password
        });
        oldLoginResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsBadRequest()
    {
        var (_, _, _, accessToken) = await RegisterConfirmedUserAsync();

        var request = AuthRequest(HttpMethod.Post, "/api/v1/auth/change-password", accessToken);
        request.Content = JsonContent.Create(new
        {
            currentPassword = "WrongPassword!",
            newPassword = "NewPassword456!"
        }, options: JsonOptions);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "Test",
            newPassword = "Test2"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────── Forgot / Reset Password ────────────

    [Fact]
    public async Task ForgotPassword_ExistingEmail_ReturnsNoContent()
    {
        var (_, email, _, _) = await RegisterUserAsync();

        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/forgot-password", new { email });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify a reset token was created in the DB
        await using var db = _fixture.CreateDbContext();
        var resetToken = await db.PasswordResetTokens.AnyAsync();
        resetToken.Should().BeTrue();
    }

    [Fact]
    public async Task ForgotPassword_NonexistentEmail_ReturnsNoContent()
    {
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/forgot-password", new
        {
            email = $"nonexistent_{Guid.NewGuid():N}@xcord.local"
        });

        // Always returns 204 to prevent user enumeration
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ChangesPasswordAndInvalidatesRefreshTokens()
    {
        var (userId, email, _, _) = await RegisterUserAsync();

        // Create a known reset token in the DB
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = ComputeSha256(rawToken);

        await using (var db = _fixture.CreateDbContext())
        {
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), // simple unique ID
                UserId = userId,
                TokenHash = tokenHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Reset password
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/reset-password", new
        {
            token = rawToken,
            newPassword = "ResetPassword789!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // All pre-existing refresh tokens should be invalidated
        await using var db2 = _fixture.CreateDbContext();
        var tokenCount = await db2.RefreshTokens.CountAsync(rt => rt.UserId == userId);
        tokenCount.Should().Be(0);

        // Verify login with new password works
        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "ResetPassword789!"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/reset-password", new
        {
            token = "completely-invalid-token",
            newPassword = "NewPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────── Two-Factor Authentication ────────────

    [Fact]
    public async Task TwoFactor_FullFlow_Enable_Login_Verify()
    {
        // 1. Register and confirm email (required for 2FA)
        var (userId, email, password, accessToken) = await RegisterConfirmedUserAsync();

        // 2. Enable 2FA (generates code)
        var enableRequest = AuthRequest(HttpMethod.Post, "/api/v1/auth/2fa/enable", accessToken);
        var enableResponse = await _fixture.Client.SendAsync(enableRequest);
        enableResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Read the 2FA code from DB and confirm enable
        string twoFactorCode;
        await using (var db = _fixture.CreateDbContext())
        {
            var code = await db.TwoFactorCodes
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
            twoFactorCode = code.Code;
        }

        var confirmRequest = AuthRequest(HttpMethod.Post, $"/api/v1/auth/2fa/confirm-enable?code={twoFactorCode}", accessToken);
        var confirmResponse = await _fixture.Client.SendAsync(confirmRequest);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Login should now require 2FA
        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new { email, password });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginBody = await loginResponse.ReadAsJsonAsync<JsonElement>();
        loginBody.GetProperty("requiresTwoFactor").GetBoolean().Should().BeTrue();
        var twoFactorToken = loginBody.GetProperty("twoFactorToken").GetString();
        twoFactorToken.Should().NotBeNullOrEmpty();

        // No refresh token cookie should be set yet
        ExtractRefreshTokenCookie(loginResponse).Should().BeNull();

        // 5. Read the login-generated 2FA code from DB
        string loginTwoFactorCode;
        await using (var db = _fixture.CreateDbContext())
        {
            var code = await db.TwoFactorCodes
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
            loginTwoFactorCode = code.Code;
        }

        // 6. Verify 2FA with code + token
        var verifyResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/2fa/verify", new
        {
            code = loginTwoFactorCode,
            twoFactorToken
        });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var verifyBody = await verifyResponse.ReadAsJsonAsync<JsonElement>();
        TestHelper.ParseSnowflakeId(verifyBody.GetProperty("userId")).Should().Be(userId);
        verifyBody.GetProperty("authenticated").GetBoolean().Should().BeTrue();
        ExtractAccessTokenCookie(verifyResponse).Should().NotBeNullOrEmpty();
        ExtractRefreshTokenCookie(verifyResponse).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TwoFactorEnable_WithoutConfirmedEmail_ReturnsForbidden()
    {
        var (_, _, _, accessToken) = await RegisterUserAsync();

        var request = AuthRequest(HttpMethod.Post, "/api/v1/auth/2fa/enable", accessToken);
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────── Logout ────────────

    [Fact]
    public async Task Logout_ClearsRefreshTokenFromDatabase()
    {
        var (userId, email, password, accessToken) = await RegisterUserAsync();

        // Login to get a fresh refresh token
        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new { email, password });
        var refreshToken = ExtractRefreshTokenCookie(loginResponse);
        refreshToken.Should().NotBeNullOrEmpty();

        // Logout with both auth header and cookie
        var request = AuthRequest(HttpMethod.Post, "/api/v1/auth/logout", accessToken);
        request.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the specific refresh token was removed from DB
        var tokenHash = ComputeSha256(refreshToken!);
        await using var db = _fixture.CreateDbContext();
        var exists = await db.RefreshTokens.AnyAsync(rt => rt.TokenHash == tokenHash);
        exists.Should().BeFalse();
    }

    // ──────────── End-to-End Flow ────────────

    [Fact]
    public async Task FullFlow_Register_ConfirmEmail_Login_Refresh_ChangePassword()
    {
        // Register
        var (userId, email, password, accessToken) = await RegisterUserAsync();

        // Confirm email
        await ConfirmEmailForUser(userId, accessToken);

        // Login
        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new { email, password });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginAccessToken = ExtractAccessTokenCookie(loginResponse)!;
        var refreshToken = ExtractRefreshTokenCookie(loginResponse)!;

        // Refresh token
        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        var refreshResponse = await _fixture.Client.SendAsync(refreshRequest);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refreshResponse.ReadAsJsonAsync<JsonElement>();
        refreshBody.GetProperty("authenticated").GetBoolean().Should().BeTrue();
        var newAccessToken = ExtractAccessTokenCookie(refreshResponse)!;
        newAccessToken.Should().NotBeNullOrEmpty();

        // Change password (using the new access token)
        var changePwRequest = AuthRequest(HttpMethod.Post, "/api/v1/auth/change-password", newAccessToken);
        changePwRequest.Content = JsonContent.Create(new
        {
            currentPassword = password,
            newPassword = "ChangedPassword999!"
        }, options: JsonOptions);
        var changePwResponse = await _fixture.Client.SendAsync(changePwRequest);
        changePwResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Login with new password
        var finalLoginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "ChangedPassword999!"
        });
        finalLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ──────────── Helpers ────────────

    /// <summary>
    /// Registers a new user, confirms email, and returns (userId, email, password, accessToken)
    /// with email_confirmed=true.
    /// </summary>
    private async Task<(long UserId, string Email, string Password, string AccessToken)> RegisterConfirmedUserAsync()
    {
        var (userId, email, password, accessToken) = await RegisterUserAsync();
        await ConfirmEmailForUser(userId, accessToken);

        // Login to get a fresh token with email_confirmed=true
        var loginResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var confirmedAccessToken = ExtractAccessTokenCookie(loginResponse)!;

        return (userId, email, password, confirmedAccessToken);
    }

    /// <summary>
    /// Registers a new user and returns (userId, email, password, accessToken) with email_confirmed=false.
    /// </summary>
    private async Task<(long UserId, string Email, string Password, string AccessToken)> RegisterUserAsync()
    {
        var (username, email) = UniqueCredentials();
        var password = "TestPassword123!";

        var response = await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username,
            displayName = "Test User",
            email,
            password
        });

        response.EnsureSuccessStatusCode();

        var body = await response.ReadAsJsonAsync<JsonElement>();
        var userId = TestHelper.ParseSnowflakeId(body.GetProperty("userId"));
        var accessToken = ExtractAccessTokenCookie(response)!;

        return (userId, email, password, accessToken);
    }

    /// <summary>
    /// Confirms email for a user by reading the code directly from the database.
    /// </summary>
    private async Task ConfirmEmailForUser(long userId, string accessToken)
    {
        await using var db = _fixture.CreateDbContext();
        var token = await db.EmailConfirmationTokens
            .FirstOrDefaultAsync(t => t.UserId == userId);

        token.Should().NotBeNull("an email confirmation token should exist for the user");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/confirm-email?code={token!.Code}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static (string Username, string Email) UniqueCredentials()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        return ($"user_{id}", $"{id}@xcord.local");
    }

    private static HttpRequestMessage AuthRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static string? ExtractRefreshTokenCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        foreach (var cookie in cookies)
        {
            if (cookie.StartsWith("refresh_token=", StringComparison.OrdinalIgnoreCase))
            {
                var value = cookie.Split(';')[0]["refresh_token=".Length..];
                return string.IsNullOrEmpty(value) ? null : value;
            }
        }

        return null;
    }

    private static string? ExtractAccessTokenCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        foreach (var cookie in cookies)
        {
            if (cookie.StartsWith("access_token=", StringComparison.OrdinalIgnoreCase))
            {
                var value = cookie.Split(';')[0]["access_token=".Length..];
                return string.IsNullOrEmpty(value) ? null : value;
            }
        }

        return null;
    }

    private static string ComputeSha256(string input)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }
}
