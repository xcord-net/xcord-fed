using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

namespace Xcord.Tests.Architecture;

public sealed class StructuralTests
{
    private const string DomainNamespace = "Xcord.Entities";

    [Fact]
    public void Entities_ShouldHaveIdPropertyOfTypeLong()
    {
        // Arrange
        var assembly = typeof(Xcord.Entities.User).Assembly;

        // Act
        var entityTypes = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(DomainNamespace)
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.IsEnum);

        // Assert
        foreach (var entityType in entityTypes)
        {
            var idProperty = entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

            // Some entities are join tables without Id (e.g., DmChannelMember, MemberRole, ThreadMember)
            // Skip those - they use composite keys
            if (idProperty == null)
                continue;

            idProperty.PropertyType.Should().Be(typeof(long), $"{entityType.Name}.Id should be of type long");
        }
    }

    [Fact]
    public void SoftDeletableEntities_ShouldImplementISoftDeletable()
    {
        // Arrange
        var assembly = typeof(Xcord.Entities.User).Assembly;
        var softDeletableInterface = typeof(Xcord.ISoftDeletable);

        // Act
        var entitiesWithDeletedAt = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(DomainNamespace)
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => t.GetProperty("DeletedAt") != null)
            .ToList();

        // Assert
        foreach (var entityType in entitiesWithDeletedAt)
        {
            entityType.GetInterfaces().Should().Contain(softDeletableInterface,
                $"{entityType.Name} has DeletedAt property and should implement ISoftDeletable");
        }
    }

    [Fact]
    public void Entities_ShouldBeSealed()
    {
        // Arrange
        var assembly = typeof(Xcord.Entities.User).Assembly;

        // Act
        var entityTypes = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(DomainNamespace)
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.IsEnum);

        // Assert
        foreach (var entityType in entityTypes)
        {
            entityType.IsSealed.Should().BeTrue($"{entityType.Name} should be sealed");
        }
    }

    [Fact]
    public void Entities_ShouldHavePublicParameterlessConstructor()
    {
        // Arrange
        var assembly = typeof(Xcord.Entities.User).Assembly;

        // Act
        var entityTypes = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(DomainNamespace)
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.IsEnum);

        // Assert
        foreach (var entityType in entityTypes)
        {
            var parameterlessConstructor = entityType.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);

            parameterlessConstructor.Should().NotBeNull(
                $"{entityType.Name} should have a public parameterless constructor for EF Core");
        }
    }

    [Fact]
    public void ISoftDeletableImplementations_ShouldHaveDeletedAtProperty()
    {
        // Arrange
        var assembly = typeof(Xcord.Entities.User).Assembly;
        var softDeletableInterface = typeof(Xcord.ISoftDeletable);

        // Act
        var softDeletableTypes = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(DomainNamespace)
            .And()
            .AreClasses()
            .GetTypes()
            .Where(t => softDeletableInterface.IsAssignableFrom(t))
            .ToList();

        // Assert
        foreach (var entityType in softDeletableTypes)
        {
            var deletedAtProperty = entityType.GetProperty("DeletedAt", BindingFlags.Public | BindingFlags.Instance);
            deletedAtProperty.Should().NotBeNull($"{entityType.Name} implements ISoftDeletable and should have DeletedAt property");
            deletedAtProperty!.PropertyType.Should().Be(typeof(DateTimeOffset?),
                $"{entityType.Name}.DeletedAt should be of type DateTimeOffset?");
        }
    }

    [Fact]
    public void DomainEntities_ShouldNotDependOnInfrastructure()
    {
        // Arrange
        var assembly = typeof(Xcord.Entities.User).Assembly;

        // Act
        var result = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(DomainNamespace)
            .ShouldNot()
            .HaveDependencyOn("Xcord.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue("Domain entities should not depend on Infrastructure");
    }

    [Fact]
    public void DomainEntities_ShouldNotDependOnFeatures()
    {
        // Arrange
        var assembly = typeof(Xcord.Entities.User).Assembly;

        // Act
        var result = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(DomainNamespace)
            .ShouldNot()
            .HaveDependencyOn("Xcord.Features")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue("Domain entities should not depend on Features");
    }

    [Fact]
    public void Enums_ShouldResideInEntitiesNamespace()
    {
        // Arrange
        var assembly = typeof(Xcord.Entities.Permission).Assembly;

        // Act
        var enumTypes = assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace != null && t.Namespace.StartsWith("Xcord.Entities"))
            .ToList();

        // Assert - all domain enums should be in Xcord.Entities namespace
        foreach (var enumType in enumTypes)
        {
            enumType.Namespace.Should().Be("Xcord.Entities",
                $"{enumType.Name} should be in Xcord.Entities namespace");
        }
    }
}
