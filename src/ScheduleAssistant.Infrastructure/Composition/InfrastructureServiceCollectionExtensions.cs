using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScheduleAssistant.Application.Abstractions.Attachments;
using ScheduleAssistant.Application.Abstractions.Configuration;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Abstractions.Settings;
using ScheduleAssistant.Application;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Infrastructure.Logging;
using ScheduleAssistant.Infrastructure.FileSystem;
using ScheduleAssistant.Infrastructure.Persistence;
using ScheduleAssistant.Infrastructure.Persistence.Repositories;
using ScheduleAssistant.Infrastructure.Settings;
using ScheduleAssistant.Application.Reminders;
using ScheduleAssistant.Infrastructure.Reminders;
using ScheduleAssistant.Infrastructure.Notifications;

namespace ScheduleAssistant.Infrastructure.Composition;

/// <summary>
/// Registers the Infrastructure composition boundary.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the persistence adapters owned by Infrastructure.
    /// </summary>
    /// <param name="services">The service collection owned by the application composition root.</param>
    /// <param name="rootDirectory">Optional injectable application root, primarily for tests.</param>
    /// <returns>The same service collection for fluent composition.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? rootDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = typeof(ApplicationAssemblyMarker);
        _ = typeof(DomainAssemblyMarker);

        services.AddSingleton<AppPaths>(_ => rootDirectory is null ? AppPaths.CreateDefault() : new AppPaths(rootDirectory));
        services.AddSingleton<IAppPaths>(serviceProvider => serviceProvider.GetRequiredService<AppPaths>());
        services.AddSingleton<IUserSettingsStore, JsonUserSettingsStore>();
        services.AddSingleton<FileLoggerOptions>();
        services.AddSingleton<FileLoggerProvider>();
        services.AddSingleton<ILoggerProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<FileLoggerProvider>());
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<SqlitePersistenceTransactionFactory>();
        services.AddSingleton<IPersistenceTransactionFactory>(serviceProvider =>
            serviceProvider.GetRequiredService<SqlitePersistenceTransactionFactory>());
        services.AddSingleton<SqliteDatabaseInitializer>();
        services.AddSingleton<SqliteTaskRepository>();
        services.AddSingleton<ITaskRepository>(serviceProvider =>
            new PersistenceMappingTaskRepository(serviceProvider.GetRequiredService<SqliteTaskRepository>()));
        services.AddSingleton<SqliteCategoryRepository>();
        services.AddSingleton<ICategoryRepository>(serviceProvider =>
            new PersistenceMappingCategoryRepository(serviceProvider.GetRequiredService<SqliteCategoryRepository>()));
        services.AddSingleton<SqliteRecurrenceSeriesRepository>();
        services.AddSingleton<IRecurrenceSeriesRepository>(serviceProvider =>
            new PersistenceMappingRecurrenceSeriesRepository(
                serviceProvider.GetRequiredService<SqliteRecurrenceSeriesRepository>()));
        services.AddSingleton<IOneShotTimerFactory, TimeProviderOneShotTimerFactory>();
        services.AddSingleton<WindowsNotificationService>();
        services.AddSingleton<INotificationService>(serviceProvider =>
            serviceProvider.GetRequiredService<WindowsNotificationService>());
        services.AddSingleton<INotificationActivationSource>(serviceProvider =>
            serviceProvider.GetRequiredService<WindowsNotificationService>());
        services.AddSingleton<SqliteReminderRepository>();
        services.AddSingleton<IReminderRepository>(serviceProvider =>
            new PersistenceMappingReminderRepository(serviceProvider.GetRequiredService<SqliteReminderRepository>()));
        services.AddSingleton<SqliteAttachmentRepository>();
        services.AddSingleton<IAttachmentRepository>(serviceProvider =>
            new PersistenceMappingAttachmentRepository(serviceProvider.GetRequiredService<SqliteAttachmentRepository>()));
        services.AddSingleton<SqliteAttachmentCleanupQueue>();
        services.AddSingleton<IAttachmentCleanupQueue>(serviceProvider =>
            serviceProvider.GetRequiredService<SqliteAttachmentCleanupQueue>());
        services.AddSingleton<IAttachmentStore, ManagedAttachmentStore>();
        services.AddSingleton<SqliteRecurrenceExclusionRepository>();
        services.AddSingleton<IRecurrenceExclusionRepository>(serviceProvider =>
            new PersistenceMappingRecurrenceExclusionRepository(
                serviceProvider.GetRequiredService<SqliteRecurrenceExclusionRepository>()));
        services.AddSingleton<IRecurrenceTaskRepository>(serviceProvider =>
            new PersistenceMappingRecurrenceTaskRepository(
                serviceProvider.GetRequiredService<SqliteTaskRepository>()));

        return services;
    }
}
