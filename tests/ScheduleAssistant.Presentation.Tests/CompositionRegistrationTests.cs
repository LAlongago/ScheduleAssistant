using Microsoft.Extensions.DependencyInjection;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Infrastructure.Composition;
using ScheduleAssistant.Presentation.Composition;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class CompositionRegistrationTests
{
    [Fact]
    public void AddPresentation_WhenResolvingApplicationPorts_ShouldUseOneTaskUseCasesAndEventBusInstance()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "ScheduleAssistant-DEV042-" + Guid.NewGuid().ToString("N"));
        using var provider = new ServiceCollection()
            .AddInfrastructure(rootDirectory)
            .AddPresentation()
            .BuildServiceProvider();

        var useCases = provider.GetRequiredService<ITaskUseCases>();
        var queries = provider.GetRequiredService<ITaskQueries>();
        var publisher = provider.GetRequiredService<IApplicationEventPublisher>();
        var bus = provider.GetRequiredService<InProcessEventBus>();

        Assert.Same(useCases, queries);
        Assert.Same(bus, publisher);
    }
}
