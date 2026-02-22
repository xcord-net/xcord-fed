using FluentAssertions;
using Xcord.Entities;

namespace Xcord.Tests.Unit;

public sealed class PermissionTests
{
    [Fact]
    public void SinglePermission_ShouldBeDetected()
    {
        // Arrange
        var permissions = Permission.SendMessages;

        // Act & Assert
        permissions.HasFlag(Permission.SendMessages).Should().BeTrue();
        permissions.HasFlag(Permission.ManageMessages).Should().BeFalse();
    }

    [Fact]
    public void CombinedPermissions_ShouldSupportOrOperation()
    {
        // Arrange
        var permissions = Permission.SendMessages | Permission.EmbedLinks | Permission.AttachFiles;

        // Act & Assert
        permissions.HasFlag(Permission.SendMessages).Should().BeTrue();
        permissions.HasFlag(Permission.EmbedLinks).Should().BeTrue();
        permissions.HasFlag(Permission.AttachFiles).Should().BeTrue();
        permissions.HasFlag(Permission.ManageMessages).Should().BeFalse();
    }

    [Fact]
    public void Administrator_ShouldBeDistinctFlag()
    {
        // Arrange
        var adminPermissions = Permission.Administrator;
        var regularPermissions = Permission.SendMessages | Permission.ViewChannels;

        // Act & Assert
        adminPermissions.HasFlag(Permission.Administrator).Should().BeTrue();
        regularPermissions.HasFlag(Permission.Administrator).Should().BeFalse();
    }

    [Fact]
    public void Administrator_WithOtherPermissions_ShouldContainBoth()
    {
        // Arrange
        var permissions = Permission.Administrator | Permission.ManageServer;

        // Act & Assert
        permissions.HasFlag(Permission.Administrator).Should().BeTrue();
        permissions.HasFlag(Permission.ManageServer).Should().BeTrue();
    }

    [Fact]
    public void TextPermissions_ShouldBeCombinableIndependently()
    {
        // Arrange
        var textPerms = Permission.SendMessages
            | Permission.EmbedLinks
            | Permission.AttachFiles
            | Permission.AddReactions
            | Permission.ReadMessageHistory;

        // Act & Assert
        textPerms.HasFlag(Permission.SendMessages).Should().BeTrue();
        textPerms.HasFlag(Permission.EmbedLinks).Should().BeTrue();
        textPerms.HasFlag(Permission.AttachFiles).Should().BeTrue();
        textPerms.HasFlag(Permission.AddReactions).Should().BeTrue();
        textPerms.HasFlag(Permission.ReadMessageHistory).Should().BeTrue();
        textPerms.HasFlag(Permission.ManageMessages).Should().BeFalse();
    }

    [Fact]
    public void VoicePermissions_ShouldBeCombinableIndependently()
    {
        // Arrange
        var voicePerms = Permission.Connect
            | Permission.Speak
            | Permission.Video
            | Permission.ShareScreen;

        // Act & Assert
        voicePerms.HasFlag(Permission.Connect).Should().BeTrue();
        voicePerms.HasFlag(Permission.Speak).Should().BeTrue();
        voicePerms.HasFlag(Permission.Video).Should().BeTrue();
        voicePerms.HasFlag(Permission.ShareScreen).Should().BeTrue();
        voicePerms.HasFlag(Permission.MuteMembers).Should().BeFalse();
    }

    [Fact]
    public void ModerationPermissions_ShouldBeCombinableIndependently()
    {
        // Arrange
        var modPerms = Permission.KickMembers
            | Permission.BanMembers
            | Permission.TimeoutMembers
            | Permission.ManageMessages;

        // Act & Assert
        modPerms.HasFlag(Permission.KickMembers).Should().BeTrue();
        modPerms.HasFlag(Permission.BanMembers).Should().BeTrue();
        modPerms.HasFlag(Permission.TimeoutMembers).Should().BeTrue();
        modPerms.HasFlag(Permission.ManageMessages).Should().BeTrue();
    }

    [Fact]
    public void AllPermissionsCombined_ShouldContainEachFlag()
    {
        // Arrange
        var allPerms = Permission.ViewChannels
            | Permission.ManageChannels
            | Permission.ManageRoles
            | Permission.SendMessages
            | Permission.Connect
            | Permission.Administrator;

        // Act & Assert
        allPerms.HasFlag(Permission.ViewChannels).Should().BeTrue();
        allPerms.HasFlag(Permission.ManageChannels).Should().BeTrue();
        allPerms.HasFlag(Permission.ManageRoles).Should().BeTrue();
        allPerms.HasFlag(Permission.SendMessages).Should().BeTrue();
        allPerms.HasFlag(Permission.Connect).Should().BeTrue();
        allPerms.HasFlag(Permission.Administrator).Should().BeTrue();
    }

    [Fact]
    public void Permission_ShouldBeLongBitfield()
    {
        // Arrange & Act
        var adminValue = (long)Permission.Administrator;

        // Assert
        // Administrator is at bit 62, so it should be 2^62
        adminValue.Should().Be(1L << 62);
    }

    [Theory]
    [InlineData(Permission.ViewChannels, 1L << 0)]
    [InlineData(Permission.ManageChannels, 1L << 3)]
    [InlineData(Permission.SendMessages, 1L << 8)]
    [InlineData(Permission.Connect, 1L << 13)]
    [InlineData(Permission.Administrator, 1L << 62)]
    public void Permission_ShouldHaveCorrectBitPosition(Permission permission, long expectedValue)
    {
        // Act
        var actualValue = (long)permission;

        // Assert
        actualValue.Should().Be(expectedValue);
    }

    [Fact]
    public void NoPermissions_ShouldBeZero()
    {
        // Arrange
        var noPermissions = (Permission)0;

        // Act & Assert
        ((long)noPermissions).Should().Be(0);
        noPermissions.HasFlag(Permission.ViewChannels).Should().BeFalse();
        noPermissions.HasFlag(Permission.Administrator).Should().BeFalse();
    }
}
