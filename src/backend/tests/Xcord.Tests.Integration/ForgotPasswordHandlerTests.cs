using System.Text.Json;
using BCrypt.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xcord.Entities;
using Xcord.Features.Auth;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;
using Xunit;

namespace Xcord.Tests.Integration;

/// <summary>
/// Integration tests for ForgotPasswordHandler and ResetPasswordHandler.
/// Verifies the password reset flow: token creation in DB, outbox entry written,
/// and full reset flow using a real PostgreSQL instance via Testcontainers.
/// </summary>
[Trait("Category", "Auth")]
public sealed class ForgotPasswordHandlerTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _redisMultiplexer;
    private string _connectionString = string.Empty;

    private const string TestEncryptionKey = "test-encryption-key-for-integration-tests";
    private const string TestRedisPrefix = "xcord-fptest";

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("xcord_forgotpw_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await Task.WhenAll(
            _postgres.StartAsync(),
            _redisContainer.StartAsync());

        _connectionString = _postgres.GetConnectionString();
        _redisMultiplexer = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());

        // Apply schema
        await using var ctx = CreateDbContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _redisMultiplexer?.Dispose();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
        if (_redisContainer is not null)
            await _redisContainer.DisposeAsync();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static PgCryptoEncryptionService CreateEncryptionService() =>
        new(TestEncryptionKey);

    /// <summary>
    /// Seeds a real User in the DB and returns their plaintext email.
    /// </summary>
    private async Task<(User user, string email)> SeedUserAsync(
        AppDbContext db,
        PgCryptoEncryptionService enc,
        string suffix = "")
    {
        var email = $"reset{suffix}_{Guid.NewGuid():N}@test.local";
        var emailHash = enc.ComputeHmac(email.ToLowerInvariant());
        var encryptedEmail = enc.Encrypt(email.ToLowerInvariant());
        var snowflake = new SnowflakeIdGenerator(7);

        var user = new User
        {
            Id = snowflake.NextId(),
            Username = $"resetuser{suffix}_{Guid.NewGuid():N}"[..30],
            DisplayName = $"Reset User {suffix}",
            Email = encryptedEmail,
            EmailHash = emailHash,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass123!", 4), // low work factor for tests
            IsAdmin = false,
            IsDisabled = false,
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user, email);
    }

    private ForgotPasswordHandler CreateForgotPasswordHandler(
        AppDbContext db,
        PgCryptoEncryptionService enc,
        string instanceDomain = "test.xcord.local")
    {
        var instanceOptions = Options.Create(new InstanceOptions
        {
            Domain = instanceDomain,
            Name = "Test Instance"
        });

        var redisOptions = Options.Create(new RedisOptions
        {
            ConnectionString = _redisContainer!.GetConnectionString(),
            ChannelPrefix = TestRedisPrefix
        });

        var snowflake = new SnowflakeIdGenerator(8);
        var outboxWriter = new OutboxWriter(new SnowflakeIdGenerator(9));

        return new ForgotPasswordHandler(
            db,
            enc,
            snowflake,
            NullLogger<ForgotPasswordHandler>.Instance,
            outboxWriter,
            instanceOptions,
            _redisMultiplexer!,
            redisOptions);
    }

    private static ResetPasswordHandler CreateResetPasswordHandler(AppDbContext db) =>
        new(db);

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_WithRegisteredEmail_CreatesResetTokenInDatabase()
    {
        // Arrange
        await using var db = CreateDbContext();
        var enc = CreateEncryptionService();

        var (user, email) = await SeedUserAsync(db, enc);
        var handler = CreateForgotPasswordHandler(db, enc);

        // Act
        var result = await handler.Handle(
            new ForgotPasswordRequest(email),
            CancellationToken.None);

        // Assert — handler returns success (bool)
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        // Verify a PasswordResetToken was persisted
        await using var verifyDb = CreateDbContext();
        var token = await verifyDb.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id);

        token.Should().NotBeNull("a reset token must be stored in the database");
        token!.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow, "token must be valid for at least some time");
        token.TokenHash.Should().NotBeNullOrEmpty("the hash must be persisted");
    }

    [Fact]
    public async Task ForgotPassword_WithRegisteredEmail_WritesOutboxEmailEntry()
    {
        // Arrange
        await using var db = CreateDbContext();
        var enc = CreateEncryptionService();

        var (_, email) = await SeedUserAsync(db, enc, "outbox");
        var handler = CreateForgotPasswordHandler(db, enc);

        // Act
        await handler.Handle(new ForgotPasswordRequest(email), CancellationToken.None);

        // Assert — an outbox event of type "Email.PasswordReset" must have been written
        await using var verifyDb = CreateDbContext();
        var outboxEntry = await verifyDb.OutboxEvents
            .FirstOrDefaultAsync(e => e.EventType == "Email.PasswordReset");

        outboxEntry.Should().NotBeNull("an Email.PasswordReset outbox entry must be written");
        outboxEntry!.Payload.Should().Contain("reset-password?token=",
            "the payload must contain the reset URL");
        outboxEntry.Payload.Should().Contain(email.ToLowerInvariant(),
            "the payload must contain the recipient email address");
        outboxEntry.ProcessedAt.Should().BeNull("the outbox entry must be unprocessed on creation");
    }

    [Fact]
    public async Task ForgotPassword_WithRegisteredEmail_ResetUrlContainsDomain()
    {
        // Arrange
        await using var db = CreateDbContext();
        var enc = CreateEncryptionService();
        const string instanceDomain = "myinstance.xcord.local";

        var (_, email) = await SeedUserAsync(db, enc, "domain");
        var handler = CreateForgotPasswordHandler(db, enc, instanceDomain);

        // Act
        await handler.Handle(new ForgotPasswordRequest(email), CancellationToken.None);

        // Assert
        await using var verifyDb = CreateDbContext();
        var outboxEntry = await verifyDb.OutboxEvents
            .FirstOrDefaultAsync(e => e.EventType == "Email.PasswordReset");

        outboxEntry.Should().NotBeNull();
        outboxEntry!.Payload.Should().Contain(instanceDomain,
            "the reset URL must include the configured instance domain");
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_ReturnsSuccessWithoutCreatingToken()
    {
        // Arrange
        await using var db = CreateDbContext();
        var enc = CreateEncryptionService();
        var handler = CreateForgotPasswordHandler(db, enc);

        var tokenCountBefore = await db.PasswordResetTokens.CountAsync();
        var outboxCountBefore = await db.OutboxEvents.CountAsync();

        // Act — email enumeration protection: always returns success
        var result = await handler.Handle(
            new ForgotPasswordRequest("nobody@notexist.example"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("handler must not leak whether the email exists");

        await using var verifyDb = CreateDbContext();
        var tokenCountAfter = await verifyDb.PasswordResetTokens.CountAsync();
        var outboxCountAfter = await verifyDb.OutboxEvents.CountAsync();

        tokenCountAfter.Should().Be(tokenCountBefore, "no token must be written for an unknown address");
        outboxCountAfter.Should().Be(outboxCountBefore, "no email must be dispatched for an unknown address");
    }

    [Fact]
    public async Task ForgotPassword_ReturnsNoContent_AlwaysRegardlessOfEmailExistence()
    {
        // Arrange
        await using var db = CreateDbContext();
        var enc = CreateEncryptionService();
        var (_, existingEmail) = await SeedUserAsync(db, enc, "always");
        var handler = CreateForgotPasswordHandler(db, enc);

        // Act — existing email
        var resultExisting = await handler.Handle(
            new ForgotPasswordRequest(existingEmail), CancellationToken.None);

        // Act — non-existing email
        await using var db2 = CreateDbContext();
        var handler2 = CreateForgotPasswordHandler(db2, enc);
        var resultNonExisting = await handler2.Handle(
            new ForgotPasswordRequest("nonexistent@nowhere.example"), CancellationToken.None);

        // Both must succeed (prevents user enumeration)
        resultExisting.IsSuccess.Should().BeTrue();
        resultNonExisting.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ForgotPassword_FullResetFlow_AllowsLoginWithNewPassword()
    {
        // Arrange — create user
        await using var db = CreateDbContext();
        var enc = CreateEncryptionService();

        var (user, email) = await SeedUserAsync(db, enc, "fullflow");
        var forgotHandler = CreateForgotPasswordHandler(db, enc);

        // Step 1: request password reset
        var forgotResult = await forgotHandler.Handle(
            new ForgotPasswordRequest(email), CancellationToken.None);
        forgotResult.IsSuccess.Should().BeTrue();

        // Step 2: extract reset token from outbox payload
        await using var outboxDb = CreateDbContext();
        var outboxEntry = await outboxDb.OutboxEvents
            .FirstOrDefaultAsync(e => e.EventType == "Email.PasswordReset");
        outboxEntry.Should().NotBeNull("outbox entry must exist after forgot password");

        var payload = JsonDocument.Parse(outboxEntry!.Payload);
        var htmlBody = payload.RootElement.GetProperty("htmlBody").GetString()!;

        var tokenStart = htmlBody.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        var tokenEnd = htmlBody.IndexOf('"', tokenStart);
        var rawToken = Uri.UnescapeDataString(htmlBody[tokenStart..tokenEnd]);

        rawToken.Should().NotBeNullOrEmpty("the reset token must be in the email body");

        // Step 3: use token to reset password
        await using var resetDb = CreateDbContext();
        var resetHandler = CreateResetPasswordHandler(resetDb);
        const string newPassword = "NewPass456!";

        var resetResult = await resetHandler.Handle(
            new ResetPasswordRequest(rawToken, newPassword),
            CancellationToken.None);

        resetResult.IsSuccess.Should().BeTrue("the token should be valid and the password should update");

        // Step 4: verify the reset token is removed (hard-deleted on use)
        await using var verifyDb = CreateDbContext();
        var usedToken = await verifyDb.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id);
        usedToken.Should().BeNull("the reset token must be hard-deleted after use");

        // Step 5: verify the updated password hash matches the new password
        var updatedUser = await verifyDb.Users
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser.Should().NotBeNull();
        BCrypt.Net.BCrypt.Verify(newPassword, updatedUser!.PasswordHash)
            .Should().BeTrue("the stored password hash must verify against the new password");
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsError()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resetHandler = CreateResetPasswordHandler(db);

        // Act
        var result = await resetHandler.Handle(
            new ResetPasswordRequest("not-a-valid-token", "NewPass456!"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("an invalid token must be rejected");
        result.Error!.Code.Should().Be("INVALID_TOKEN");
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_ReturnsError()
    {
        // Arrange
        await using var db = CreateDbContext();
        var enc = CreateEncryptionService();
        var (user, _) = await SeedUserAsync(db, enc, "expired");
        var snowflake = new SnowflakeIdGenerator(10);

        // Insert an already-expired token
        const string rawToken = "test-expired-token-12345";
        var tokenHash = TokenHelper.HashToken(rawToken);
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = snowflake.NextId(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-2), // expired 2 hours ago
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)
        });
        await db.SaveChangesAsync();

        await using var resetDb = CreateDbContext();
        var resetHandler = CreateResetPasswordHandler(resetDb);

        // Act
        var result = await resetHandler.Handle(
            new ResetPasswordRequest(rawToken, "NewPass456!"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue("an expired token must be rejected");
        result.Error!.Code.Should().Be("INVALID_TOKEN");
    }

    [Fact]
    public async Task ResetPassword_InvalidatesAllRefreshTokens()
    {
        // Arrange
        await using var db = CreateDbContext();
        var enc = CreateEncryptionService();
        var snowflake = new SnowflakeIdGenerator(11);
        var (user, _) = await SeedUserAsync(db, enc, "revokerf");

        // Seed two refresh tokens for the user
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = snowflake.NextId(),
            UserId = user.Id,
            TokenHash = "hash1",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = snowflake.NextId(),
            UserId = user.Id,
            TokenHash = "hash2",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Seed a reset token
        const string rawToken = "test-revoke-token-67890";
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = snowflake.NextId(),
            UserId = user.Id,
            TokenHash = TokenHelper.HashToken(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        await using var resetDb = CreateDbContext();
        var resetHandler = CreateResetPasswordHandler(resetDb);

        // Act
        var result = await resetHandler.Handle(
            new ResetPasswordRequest(rawToken, "NewPass789!"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateDbContext();
        var refreshCount = await verifyDb.RefreshTokens.CountAsync(rt => rt.UserId == user.Id);
        refreshCount.Should().Be(0, "all refresh tokens must be deleted after password reset");
    }
}
