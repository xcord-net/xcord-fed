using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xcord.Tests.Integration.Fixtures;
using Xcord.Tests.Integration.Helpers;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Integration tests for Redis-based per-email brute-force protection on login and forgot-password.
/// </summary>
[Collection("WebApp")]
public class LoginBruteForceTests
{
    private readonly WebAppFixture _fixture;

    public LoginBruteForceTests(WebAppFixture fixture)
    {
        _fixture = fixture;
    }

    // ──────────── Login brute-force ────────────

    [Fact]
    public async Task Login_Returns429_AfterFiveFailedAttempts()
    {
        // Register a real user so the email exists (the counter increments even for unknown emails,
        // but using a real user lets us verify the lockout fires before password check succeeds).
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"brute_{id}@xcord.local";
        var password = "TestPassword123!";

        await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username = $"brute_{id}",
            displayName = "Brute Test",
            email,
            password
        });

        // Send 5 failed attempts (wrong password)
        for (var i = 0; i < 5; i++)
        {
            var resp = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
            {
                email,
                password = "WrongPassword999!"
            });
            // Each attempt before lockout should return 400 (bad credentials)
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                $"attempt {i + 1} should fail with 400");
        }

        // 6th attempt — should now be locked out
        var lockedResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "WrongPassword999!"
        });

        lockedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "6th failed attempt should return 429 after 5 failures");

        // Verify Retry-After header is present and positive
        lockedResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues)
            .Should().BeTrue("Retry-After header must be present on 429 login responses");

        var retryAfterStr = retryAfterValues!.First();
        int.TryParse(retryAfterStr, out var retryAfterSeconds).Should().BeTrue(
            "Retry-After must be an integer number of seconds");
        retryAfterSeconds.Should().BeGreaterThan(0, "Retry-After must indicate remaining wait time");

        // Even the correct password should be blocked once the counter is at 5
        var correctPasswordBlocked = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password
        });
        correctPasswordBlocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "correct password should also be rejected while account is locked");
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetsCounter()
    {
        // Register a user
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"reset_{id}@xcord.local";
        var password = "TestPassword123!";

        await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username = $"reset_{id}",
            displayName = "Reset Test",
            email,
            password
        });

        // Send 4 failed attempts (one below the threshold)
        for (var i = 0; i < 4; i++)
        {
            await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
            {
                email,
                password = "WrongPassword999!"
            });
        }

        // Now login successfully — this should clear the counter
        var successResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password
        });
        successResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "valid login after 4 failed attempts should succeed");

        // After clearing the counter, a failed attempt should go back to 400 (not 429)
        var afterResetResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "WrongPassword999!"
        });
        afterResetResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "counter was reset by successful login — this attempt should be 400, not 429");
    }

    // ──────────── Forgot-password rate limiting ────────────

    [Fact]
    public async Task ForgotPassword_Returns204_AfterThreeAttempts_ButStopsCreatingTokens()
    {
        // Register a user so we can check whether reset tokens are created
        var id = Guid.NewGuid().ToString("N")[..12];
        var email = $"fpbrute_{id}@xcord.local";
        var password = "TestPassword123!";

        await _fixture.Client.PostJsonAsync("/api/v1/auth/register", new
        {
            username = $"fpbrute_{id}",
            displayName = "FP Brute",
            email,
            password
        });

        // First 3 requests should succeed (204) AND each should create a reset token
        for (var i = 1; i <= 3; i++)
        {
            var resp = await _fixture.Client.PostJsonAsync("/api/v1/auth/forgot-password", new { email });
            resp.StatusCode.Should().Be(HttpStatusCode.NoContent,
                $"request {i} should return 204");
        }

        // Count tokens created by the first 3 requests
        long userId;
        await using (var db = _fixture.CreateDbContext())
        {
            var user = db.Users.FirstOrDefault(u => u.Username == $"fpbrute_{id}");
            user.Should().NotBeNull();
            userId = user!.Id;
        }

        // 4th request — still 204 (no user enumeration) but no new token created
        var countBefore = await CountPasswordResetTokensAsync(userId);

        var rateLimitedResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/forgot-password", new { email });
        rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "forgot-password must ALWAYS return 204 even when rate-limited (user enumeration prevention)");

        var countAfter = await CountPasswordResetTokensAsync(userId);
        countAfter.Should().Be(countBefore,
            "no new password reset token should be created once the rate limit is exceeded");

        // 5th request — same: still 204, still no new token
        var fifthResponse = await _fixture.Client.PostJsonAsync("/api/v1/auth/forgot-password", new { email });
        fifthResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "5th request must still return 204");

        var countFinal = await CountPasswordResetTokensAsync(userId);
        countFinal.Should().Be(countBefore,
            "no additional tokens should be created after rate limit is exceeded");
    }

    [Fact]
    public async Task ForgotPassword_NonexistentEmail_Returns204_Always()
    {
        var email = $"nonexistent_{Guid.NewGuid():N}@xcord.local";

        // Even after many attempts on a non-existent email, always returns 204
        for (var i = 0; i < 5; i++)
        {
            var resp = await _fixture.Client.PostJsonAsync("/api/v1/auth/forgot-password", new { email });
            resp.StatusCode.Should().Be(HttpStatusCode.NoContent,
                $"request {i + 1} for non-existent email should always return 204");
        }
    }

    // ──────────── Helpers ────────────

    private async Task<int> CountPasswordResetTokensAsync(long userId)
    {
        await using var db = _fixture.CreateDbContext();
        return db.PasswordResetTokens.Count(t => t.UserId == userId);
    }
}
