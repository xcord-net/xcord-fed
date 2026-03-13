using FluentAssertions;
using Xcord;

namespace Xcord.Tests.Unit;

public sealed class SnowflakeIdGeneratorTests
{
    [Fact]
    public void NextId_ShouldGenerateMultipleUniqueIdsInSequence()
    {
        // Arrange
        var generator = new SnowflakeIdGenerator(workerId: 5);
        var ids = new HashSet<long>();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            ids.Add(generator.NextId());
        }

        // Assert
        ids.Should().HaveCount(1000);
    }

    [Fact]
    public void Constructor_ShouldThrowForInvalidWorkerId()
    {
        // Act & Assert
        Action actNegative = () => new SnowflakeIdGenerator(workerId: -1);
        Action actTooLarge = () => new SnowflakeIdGenerator(workerId: 1024);

        actNegative.Should().Throw<ArgumentOutOfRangeException>();
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ShouldAcceptValidWorkerId()
    {
        // Act - boundary worker IDs should not throw
        var generator1 = new SnowflakeIdGenerator(workerId: 0);
        var generator2 = new SnowflakeIdGenerator(workerId: 1023);

        // Assert - generated IDs embed the correct worker ID
        var id1 = generator1.NextId();
        var id2 = generator2.NextId();
        var extractedWorkerId1 = (id1 >> 12) & 0x3FF;
        var extractedWorkerId2 = (id2 >> 12) & 0x3FF;
        extractedWorkerId1.Should().Be(0);
        extractedWorkerId2.Should().Be(1023);
    }

    [Fact]
    public void NextId_ShouldGenerateMonotonicallyIncreasingIds()
    {
        // Arrange
        var generator = new SnowflakeIdGenerator(workerId: 10);
        var previousId = 0L;

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            var id = generator.NextId();
            id.Should().BeGreaterThan(previousId);
            previousId = id;
        }
    }

    [Fact]
    public void NextId_ShouldEmbedWorkerIdCorrectly()
    {
        // Arrange
        const int workerId = 42;
        var generator = new SnowflakeIdGenerator(workerId: workerId);

        // Act
        var id = generator.NextId();

        // Assert
        // Extract worker ID from the generated ID (bits 12-21)
        var extractedWorkerId = (id >> 12) & 0x3FF; // 0x3FF = 1023 (max worker ID)
        extractedWorkerId.Should().Be(workerId);
    }

    [Fact]
    public void NextId_ShouldEmbedTimestampCorrectly()
    {
        // Arrange
        var customEpoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var generator = new SnowflakeIdGenerator(workerId: 1, epoch: customEpoch);
        var beforeGeneration = DateTimeOffset.UtcNow;

        // Act
        var id = generator.NextId();
        var afterGeneration = DateTimeOffset.UtcNow;

        // Assert
        // Extract timestamp from the ID (bits 22-63)
        var timestampMs = id >> 22;
        var extractedTime = customEpoch.AddMilliseconds(timestampMs);

        // Allow 10ms tolerance for timestamp precision
        extractedTime.Should().BeCloseTo(beforeGeneration, TimeSpan.FromMilliseconds(10));
        extractedTime.Should().BeOnOrBefore(afterGeneration.AddMilliseconds(10));
    }

    [Fact]
    public void NextId_DifferentGenerators_ShouldProduceDifferentIds()
    {
        // Arrange
        var generator1 = new SnowflakeIdGenerator(workerId: 1);
        var generator2 = new SnowflakeIdGenerator(workerId: 2);

        // Act
        var id1 = generator1.NextId();
        var id2 = generator2.NextId();

        // Assert
        id1.Should().NotBe(id2);
    }
}
