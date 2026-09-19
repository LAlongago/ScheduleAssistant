using Microsoft.Extensions.DependencyInjection;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Registers the presentation composition graph without registering business fakes.</summary>
public static class PresentationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the single-window shell, navigation state, and page ViewModels.
    /// No ITaskUseCases, repository, or design-data persistence service is registered here.
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);
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
