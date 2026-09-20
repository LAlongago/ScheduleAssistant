using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Infrastructure.Persistence;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Registers the presentation composition graph without registering business fakes.</summary>
public static class PresentationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the single-window shell, navigation state, and page ViewModels.
    /// Application use cases and the one-shot database initialization gate are also
    /// registered here because this is the process composition root.
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<InProcessEventBus>();
        services.AddSingleton<IApplicationEventPublisher>(serviceProvider =>
            serviceProvider.GetRequiredService<InProcessEventBus>());
        services.AddSingleton<TaskUseCases>();
        services.AddSingleton<ITaskUseCases>(serviceProvider =>
            serviceProvider.GetRequiredService<TaskUseCases>());
        services.AddSingleton<ITaskQueries>(serviceProvider =>
            serviceProvider.GetRequiredService<TaskUseCases>());
        services.AddSingleton<IDatabaseInitialization, DatabaseInitialization>();
        services.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Dispatcher.CurrentDispatcher));
        services.AddTransient<IDeadlineRefreshTimer, DeadlineRefreshTimer>();
        services.AddSingleton<ITaskCardMapper, TaskCardMapper>();
        services.AddSingleton<TodayPageViewModel>();
        services.AddSingleton<WeekPageViewModel>();
        services.AddSingleton<MonthPageViewModel>();
        services.AddSingleton<UpcomingDeadlinesPageViewModel>();
        services.AddSingleton<AllTasksPageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
