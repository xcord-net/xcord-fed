using FluentAssertions;
using Xcord.Entities;

namespace Xcord.Tests.Unit;

public sealed class RoleTests
{
    [Fact]
    public void SingleRole_ShouldBeDetected()
    {
        // Arrange
        var roles = Role.SendMessages;

        // Act & Assert
        roles.HasFlag(Role.SendMessages).Should().BeTrue();
        roles.HasFlag(Role.ManageMessages).Should().BeFalse();
    }

    [Fact]
    public void CombinedRoles_ShouldSupportOrOperation()
    {
        // Arrange
        var roles = Role.SendMessages | Role.EmbedLinks | Role.AttachFiles;

        // Act & Assert
        roles.HasFlag(Role.SendMessages).Should().BeTrue();
        roles.HasFlag(Role.EmbedLinks).Should().BeTrue();
        roles.HasFlag(Role.AttachFiles).Should().BeTrue();
        roles.HasFlag(Role.ManageMessages).Should().BeFalse();
    }

    [Fact]
    public void Administrator_ShouldBeDistinctFlag()
    {
        // Arrange
        var adminRoles = Role.Administrator;
        var regularRoles = Role.SendMessages | Role.ViewChannels;

        // Act & Assert
        adminRoles.HasFlag(Role.Administrator).Should().BeTrue();
        regularRoles.HasFlag(Role.Administrator).Should().BeFalse();
    }

    [Fact]
    public void Administrator_WithOtherRoles_ShouldContainBoth()
    {
        // Arrange
        var roles = Role.Administrator | Role.ManageServer;

        // Act & Assert
        roles.HasFlag(Role.Administrator).Should().BeTrue();
        roles.HasFlag(Role.ManageServer).Should().BeTrue();
    }

    [Fact]
    public void TextRoles_ShouldBeCombinableIndependently()
    {
        // Arrange
        var textRoles = Role.SendMessages
            | Role.EmbedLinks
            | Role.AttachFiles
            | Role.AddReactions
            | Role.ReadMessageHistory;

        // Act & Assert
        textRoles.HasFlag(Role.SendMessages).Should().BeTrue();
        textRoles.HasFlag(Role.EmbedLinks).Should().BeTrue();
        textRoles.HasFlag(Role.AttachFiles).Should().BeTrue();
        textRoles.HasFlag(Role.AddReactions).Should().BeTrue();
        textRoles.HasFlag(Role.ReadMessageHistory).Should().BeTrue();
        textRoles.HasFlag(Role.ManageMessages).Should().BeFalse();
    }

    [Fact]
    public void VoiceRoles_ShouldBeCombinableIndependently()
    {
        // Arrange
        var voiceRoles = Role.Connect
            | Role.Speak
            | Role.Video
            | Role.ShareScreen;

        // Act & Assert
        voiceRoles.HasFlag(Role.Connect).Should().BeTrue();
        voiceRoles.HasFlag(Role.Speak).Should().BeTrue();
        voiceRoles.HasFlag(Role.Video).Should().BeTrue();
        voiceRoles.HasFlag(Role.ShareScreen).Should().BeTrue();
        voiceRoles.HasFlag(Role.MuteMembers).Should().BeFalse();
    }

    [Fact]
    public void ModerationRoles_ShouldBeCombinableIndependently()
    {
        // Arrange
        var modRoles = Role.KickMembers
            | Role.BanMembers
            | Role.TimeoutMembers
            | Role.ManageMessages;

        // Act & Assert
        modRoles.HasFlag(Role.KickMembers).Should().BeTrue();
        modRoles.HasFlag(Role.BanMembers).Should().BeTrue();
        modRoles.HasFlag(Role.TimeoutMembers).Should().BeTrue();
        modRoles.HasFlag(Role.ManageMessages).Should().BeTrue();
    }

    [Fact]
    public void AllRolesCombined_ShouldContainEachFlag()
    {
        // Arrange
        var allRoles = Role.ViewChannels
            | Role.ManageChannels
            | Role.ManageGroups
            | Role.SendMessages
            | Role.Connect
            | Role.Administrator;

        // Act & Assert
        allRoles.HasFlag(Role.ViewChannels).Should().BeTrue();
        allRoles.HasFlag(Role.ManageChannels).Should().BeTrue();
        allRoles.HasFlag(Role.ManageGroups).Should().BeTrue();
        allRoles.HasFlag(Role.SendMessages).Should().BeTrue();
        allRoles.HasFlag(Role.Connect).Should().BeTrue();
        allRoles.HasFlag(Role.Administrator).Should().BeTrue();
    }

    [Fact]
    public void Role_ShouldBeLongBitfield()
    {
        // Arrange & Act
        var adminValue = (long)Role.Administrator;

        // Assert
        // Administrator is at bit 62, so it should be 2^62
        adminValue.Should().Be(1L << 62);
    }

    [Theory]
    [InlineData(Role.ViewChannels, 1L << 0)]
    [InlineData(Role.ManageChannels, 1L << 3)]
    [InlineData(Role.SendMessages, 1L << 8)]
    [InlineData(Role.Connect, 1L << 13)]
    [InlineData(Role.Administrator, 1L << 62)]
    public void Role_ShouldHaveCorrectBitPosition(Role role, long expectedValue)
    {
        // Act
        var actualValue = (long)role;

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void NoRoles_ShouldBeZero()
    {
        // Arrange
        var noRoles = (Role)0;

        // Act & Assert
        ((long)noRoles).Should().Be(0);
        noRoles.HasFlag(Role.ViewChannels).Should().BeFalse();
        noRoles.HasFlag(Role.Administrator).Should().BeFalse();
    }
}
