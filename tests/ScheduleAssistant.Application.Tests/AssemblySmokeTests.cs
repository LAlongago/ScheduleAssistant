using ScheduleAssistant.Application;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Application.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void ApplicationAssembly_ShouldReferenceDomain()
    {
        Assert.Same(typeof(DomainAssemblyMarker).Assembly, ApplicationAssemblyMarker.DomainAssembly);
    }
}
