using System.Xml.Linq;
using Xunit;

namespace ScheduleAssistant.Architecture.Tests;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void Domain_ShouldNotDependOnOtherProductionProjects()
    {
        AssertProjectReferencesExactly("ScheduleAssistant.Domain");
    }

    [Fact]
    public void Application_ShouldDependOnlyOnDomain()
    {
        AssertProjectReferencesExactly("ScheduleAssistant.Application", "ScheduleAssistant.Domain");
    }

    [Fact]
    public void Infrastructure_ShouldDependOnlyOnApplicationAndDomain()
    {
        AssertProjectReferencesExactly(
            "ScheduleAssistant.Infrastructure",
            "ScheduleAssistant.Application",
            "ScheduleAssistant.Domain");
    }

    [Fact]
    public void Presentation_ShouldDependOnApplicationAndInfrastructure()
    {
        AssertProjectReferencesExactly(
            "ScheduleAssistant.Presentation",
            "ScheduleAssistant.Application",
            "ScheduleAssistant.Infrastructure");
    }

    private static void AssertProjectReferencesExactly(
        string projectName,
        params string[] allowedProjectNames)
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var projectDirectory = Path.Combine(repositoryRoot, "src", projectName);
        var projectFilePath = Path.Combine(projectDirectory, projectName + ".csproj");
        Assert.True(File.Exists(projectFilePath), $"Could not find project file {projectFilePath}.");

        var actual = XDocument.Load(projectFilePath)
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(
                Path.GetFullPath(Path.Combine(projectDirectory, include!))))
            .ToHashSet(StringComparer.Ordinal);
        var expected = allowedProjectNames.ToHashSet(StringComparer.Ordinal);

        Assert.True(
            actual.SetEquals(expected),
            $"{projectName} declares project references [{string.Join(", ", actual.OrderBy(name => name, StringComparer.Ordinal))}], expected [{string.Join(", ", expected.OrderBy(name => name, StringComparer.Ordinal))}].");
    }

    private static string FindRepositoryRoot(string currentDirectory)
    {
        var directory = new DirectoryInfo(currentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ScheduleAssistant.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find ScheduleAssistant.sln from {currentDirectory}.");
    }
}
