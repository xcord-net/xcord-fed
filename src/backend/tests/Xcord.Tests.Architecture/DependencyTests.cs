using FluentAssertions;
using NetArchTest.Rules;

namespace Xcord.Tests.Architecture;

public sealed class DependencyTests
{
    private const string DomainNamespace = "Xcord.Domain";
    private const string SharedNamespace = "Xcord.Shared";
    private const string FeaturesNamespace = "Xcord.Features";
    private const string InfrastructureNamespace = "Xcord.Infrastructure";
    private const string ApiNamespace = "Xcord.Api";

    [Fact]
    public void Domain_ShouldNotDependOnOtherLayers()
    {
        // Arrange
        var assembly = typeof(Xcord.DomainAssemblyMarker).Assembly;

        // Act
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(FeaturesNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Features_ShouldNotDependOnApi()
    {
        // Arrange
        var assembly = typeof(Xcord.Features.FeaturesAssemblyMarker).Assembly;

        // Act
        // Note: Features CAN depend on Infrastructure (for data access and services)
        // but should NOT depend on Api (the host layer)
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Shared_ShouldNotDependOnAnyLayer()
    {
        // Arrange
        var assembly = typeof(Xcord.DomainAssemblyMarker).Assembly;

        // Act
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, FeaturesNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnApi()
    {
        // Arrange
        var assembly = typeof(Xcord.Infrastructure.Data.AppDbContext).Assembly;

        // Act
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnFeatures()
    {
        // Arrange
        var assembly = typeof(Xcord.Infrastructure.Data.AppDbContext).Assembly;

        // Act
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn(FeaturesNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }
}
