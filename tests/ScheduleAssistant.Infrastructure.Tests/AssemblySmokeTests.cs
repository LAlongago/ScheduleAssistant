using ScheduleAssistant.Application;
using ScheduleAssistant.Infrastructure;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void InfrastructureAssembly_ShouldReferenceApplication()
    {
        Assert.Same(typeof(ApplicationAssemblyMarker).Assembly, InfrastructureAssemblyMarker.ApplicationAssembly);
    }
}
