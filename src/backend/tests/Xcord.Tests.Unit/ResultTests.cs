using FluentAssertions;
using Xcord;

namespace Xcord.Tests.Unit;

public sealed class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result<int>.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        // Arrange
        var error = Error.NotFound("USER_NOT_FOUND", "User not found");

        // Act
        var result = Result<int>.Failure(error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Value_OnFailedResult_ShouldThrow()
    {
        // Arrange
        var error = Error.Validation("INVALID_INPUT", "Invalid input");
        var result = Result<int>.Failure(error);

        // Act
        Action act = () => _ = result.Value;

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Error_OnSuccessfulResult_ShouldThrow()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        Action act = () => _ = result.Error;

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Match_ShouldInvokeCorrectFunction()
    {
        // Arrange
        var successResult = Result<int>.Success(42);
        var failureResult = Result<int>.Failure(Error.NotFound("NOT_FOUND", "Not found"));

        // Act
        var successOutput = successResult.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: error => $"Error: {error.Code}");

        var failureOutput = failureResult.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: error => $"Error: {error.Code}");

        // Assert
        successOutput.Should().Be("Success: 42");
        failureOutput.Should().Be("Error: NOT_FOUND");
    }

    [Fact]
    public void ImplicitConversion_FromValue_ShouldCreateSuccessResult()
    {
        // Act
        Result<string> result = "test value";

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test value");
    }

    [Fact]
    public void ImplicitConversion_FromError_ShouldCreateFailureResult()
    {
        // Arrange
        var error = Error.Validation("INVALID", "Invalid data");

        // Act
        Result<string> result = error;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void ImplicitConversion_InMethodReturn_ShouldWork()
    {
        // Arrange
        static Result<int> GetSuccessResult() => 100;
        static Result<int> GetFailureResult() => Error.NotFound("NOT_FOUND", "Not found");

        // Act
        var successResult = GetSuccessResult();
        var failureResult = GetFailureResult();

        // Assert
        successResult.IsSuccess.Should().BeTrue();
        successResult.Value.Should().Be(100);
        failureResult.IsFailure.Should().BeTrue();
        failureResult.Error.Code.Should().Be("NOT_FOUND");
    }
}
