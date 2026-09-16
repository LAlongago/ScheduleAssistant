using System.Reflection;
using ScheduleAssistant.Application;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Infrastructure;
using ScheduleAssistant.Presentation;
using Xunit;

namespace ScheduleAssistant.Architecture.Tests;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void Domain_ShouldNotDependOnOtherProductionProjects()
    {
        AssertProjectReferencesExactly(typeof(DomainAssemblyMarker).Assembly);
    }

    [Fact]
    public void Application_ShouldDependOnlyOnDomain()
    {
        AssertProjectReferencesExactly(
            typeof(ApplicationAssemblyMarker).Assembly,
            typeof(DomainAssemblyMarker).Assembly);
    }

    [Fact]
    public void Infrastructure_ShouldDependOnlyOnApplicationAndDomain()
    {
        AssertProjectReferencesExactly(
            typeof(InfrastructureAssemblyMarker).Assembly,
            typeof(ApplicationAssemblyMarker).Assembly,
            typeof(DomainAssemblyMarker).Assembly);
    }

    [Fact]
    public void Presentation_ShouldDependOnApplicationAndInfrastructure()
    {
        AssertProjectReferencesExactly(
            typeof(App).Assembly,
            typeof(ApplicationAssemblyMarker).Assembly,
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    private static void AssertProjectReferencesExactly(Assembly assembly, params Assembly[] allowedAssemblies)
    {
        var expected = allowedAssemblies
            .Select(allowedAssembly => allowedAssembly.GetName().Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var actual = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name.StartsWith("ScheduleAssistant", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(
            actual.SetEquals(expected),
            $"{assembly.GetName().Name} references [{string.Join(", ", actual.OrderBy(name => name, StringComparer.Ordinal))}], expected [{string.Join(", ", expected.OrderBy(name => name, StringComparer.Ordinal))}].");
    }
}
