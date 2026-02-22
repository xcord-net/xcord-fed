using FluentAssertions;
using Xcord;

namespace Xcord.Tests.Unit;

public sealed class ErrorTests
{
    [Fact]
    public void NotFound_ShouldCreate404Error()
    {
        // Act
        var error = Error.NotFound("USER_NOT_FOUND", "User not found");

        // Assert
        error.Code.Should().Be("USER_NOT_FOUND");
        error.Message.Should().Be("User not found");
        error.StatusCode.Should().Be(404);
    }

    [Fact]
    public void Validation_ShouldCreate400Error()
    {
        // Act
        var error = Error.Validation("INVALID_INPUT", "Invalid input");

        // Assert
        error.Code.Should().Be("INVALID_INPUT");
        error.Message.Should().Be("Invalid input");
        error.StatusCode.Should().Be(400);
    }

    [Fact]
    public void Conflict_ShouldCreate409Error()
    {
        // Act
        var error = Error.Conflict("DUPLICATE_USER", "User already exists");

        // Assert
        error.Code.Should().Be("DUPLICATE_USER");
        error.Message.Should().Be("User already exists");
        error.StatusCode.Should().Be(409);
    }

    [Fact]
    public void Forbidden_ShouldCreate403Error()
    {
        // Act
        var error = Error.Forbidden("NO_PERMISSION", "Insufficient permissions");

        // Assert
        error.Code.Should().Be("NO_PERMISSION");
        error.Message.Should().Be("Insufficient permissions");
        error.StatusCode.Should().Be(403);
    }

    [Fact]
    public void Failure_ShouldCreate500Error()
    {
        // Act
        var error = Error.Failure("INTERNAL_ERROR", "Internal server error");

        // Assert
        error.Code.Should().Be("INTERNAL_ERROR");
        error.Message.Should().Be("Internal server error");
        error.StatusCode.Should().Be(500);
    }

    [Fact]
    public void BadRequest_ShouldCreate400Error()
    {
        // Act
        var error = Error.BadRequest("BAD_REQUEST", "Bad request");

        // Assert
        error.Code.Should().Be("BAD_REQUEST");
        error.Message.Should().Be("Bad request");
        error.StatusCode.Should().Be(400);
    }

    [Fact]
    public void RateLimited_ShouldCreate429Error()
    {
        // Act
        var error = Error.RateLimited("RATE_LIMITED", "Too many requests");

        // Assert
        error.Code.Should().Be("RATE_LIMITED");
        error.Message.Should().Be("Too many requests");
        error.StatusCode.Should().Be(429);
    }

    [Fact]
    public void ErrorRecord_ShouldSupportEquality()
    {
        // Arrange
        var error1 = Error.NotFound("NOT_FOUND", "Not found");
        var error2 = Error.NotFound("NOT_FOUND", "Not found");
        var error3 = Error.NotFound("DIFFERENT", "Not found");

        // Assert
        error1.Should().Be(error2);
        error1.Should().NotBe(error3);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(500)]
    [InlineData(429)]
    public void AllErrorFactories_ShouldMapToCorrectHttpStatusCodes(int expectedStatusCode)
    {
        // Act & Assert
        var error = expectedStatusCode switch
        {
            404 => Error.NotFound("CODE", "Message"),
            400 => Error.Validation("CODE", "Message"),
            403 => Error.Forbidden("CODE", "Message"),
            409 => Error.Conflict("CODE", "Message"),
            500 => Error.Failure("CODE", "Message"),
            429 => Error.RateLimited("CODE", "Message"),
            _ => throw new InvalidOperationException()
        };

        error.StatusCode.Should().Be(expectedStatusCode);
    }
}
