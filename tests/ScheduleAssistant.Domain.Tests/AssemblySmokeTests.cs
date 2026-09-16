using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Domain.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void DomainAssembly_ShouldBeLoadable()
    {
        Assert.Equal("ScheduleAssistant.Domain", typeof(DomainAssemblyMarker).Assembly.GetName().Name);
    }
}
