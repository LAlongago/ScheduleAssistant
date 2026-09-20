using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Infrastructure.Composition;
using ScheduleAssistant.Presentation;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.ViewModels;
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

    [Fact]
    public void AddPresentation_WhenResolvingMainWindow_ShouldResolveCompleteConstructorGraph()
    {
        Exception? failure = null;
        var resolved = false;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new System.Windows.Application();
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/ScheduleAssistant;component/Resources/Theme.Light.xaml",
                        UriKind.Absolute)
                });
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/ScheduleAssistant;component/Resources/Controls.xaml",
                        UriKind.Absolute)
                });

                var rootDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "ScheduleAssistant-INTEGRATION008-" + Guid.NewGuid().ToString("N"));
                using var provider = new ServiceCollection()
                    .AddInfrastructure(rootDirectory)
                    .AddPresentation()
                    .BuildServiceProvider();

                var window = provider.GetRequiredService<MainWindow>();

                Assert.NotNull(window.DataContext);
                Assert.IsType<MainWindowViewModel>(window.DataContext);
                Assert.Same(provider.GetRequiredService<ITaskQueries>(), provider.GetRequiredService<ITaskUseCases>());
                Assert.Same(
                    provider.GetRequiredService<InProcessEventBus>(),
                    provider.GetRequiredService<IApplicationEventPublisher>());
                resolved = true;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        Assert.True(resolved);
    }
}
