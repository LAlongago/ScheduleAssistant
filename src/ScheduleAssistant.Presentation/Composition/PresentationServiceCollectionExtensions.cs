using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows.Threading;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Attachments;
using ScheduleAssistant.Application.Recurrence;
using ScheduleAssistant.Application.Reminders;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Infrastructure.Persistence;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Registers the single Presentation composition graph and its real Application services.</summary>
public static class PresentationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the single-window shell, real task use cases, one-shot database initialization gate,
    /// navigation state, page ViewModels, and task-editor services. No repository is exposed to a
    /// ViewModel.
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TaskDeadlineResolver>();
        services.AddSingleton<InProcessEventBus>();
        services.AddSingleton<IApplicationEventPublisher>(serviceProvider =>
            serviceProvider.GetRequiredService<InProcessEventBus>());
        services.AddSingleton<RecurrenceMaterializer>();
        services.AddSingleton<IRecurrenceMaterializer>(serviceProvider =>
            serviceProvider.GetRequiredService<RecurrenceMaterializer>());
        services.AddSingleton<ITransactionalRecurrenceMaterializer>(serviceProvider =>
            serviceProvider.GetRequiredService<RecurrenceMaterializer>());
        services.AddSingleton<RecurrenceUseCases>();
        services.AddSingleton<IRecurrenceUseCases>(serviceProvider =>
            serviceProvider.GetRequiredService<RecurrenceUseCases>());
        services.AddSingleton<TaskUseCases>();
        services.AddSingleton<ITaskUseCases>(serviceProvider =>
            serviceProvider.GetRequiredService<TaskUseCases>());
        services.AddSingleton<ITaskQueries>(serviceProvider =>
            serviceProvider.GetRequiredService<TaskUseCases>());
        services.AddSingleton<INotificationService, UnavailableNotificationService>();
        services.AddSingleton<ReminderScheduler>();
        services.AddSingleton<IReminderScheduler>(serviceProvider =>
            serviceProvider.GetRequiredService<ReminderScheduler>());
        services.AddSingleton<IAttachmentUseCases, AttachmentUseCases>();
        services.AddSingleton<AttachmentMaintenanceService>();
        services.AddSingleton<IDatabaseInitialization, DatabaseInitialization>();
        services.AddHostedService<DatabaseInitializationHostedService>();
        services.AddHostedService<ReminderSchedulerHostedService>();
        services.AddHostedService<AttachmentMaintenanceHostedService>();
        services.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Dispatcher.CurrentDispatcher));
        services.AddSingleton<IDeadlineRefreshTimer, DeadlineRefreshTimer>();
        services.AddSingleton<ITaskCardMapper, TaskCardMapper>();
        services.AddSingleton<ITaskEditorInteractionService, WpfTaskEditorInteractionService>();
        services.AddSingleton<ITaskEditorWindowFactory, TaskEditorWindowFactory>();
        services.AddSingleton<ITaskEditorService, TaskEditorService>();
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
