using Microsoft.Extensions.DependencyInjection;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Infrastructure.Persistence;
using ScheduleAssistant.Infrastructure.Persistence.Repositories;

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

        services.AddSingleton(_ => rootDirectory is null ? AppPaths.CreateDefault() : new AppPaths(rootDirectory));
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<SqlitePersistenceTransactionFactory>();
        services.AddSingleton<IPersistenceTransactionFactory>(serviceProvider =>
            serviceProvider.GetRequiredService<SqlitePersistenceTransactionFactory>());
        services.AddSingleton<SqliteDatabaseInitializer>();
        services.AddSingleton<ITaskRepository, SqliteTaskRepository>();
        services.AddSingleton<ICategoryRepository, SqliteCategoryRepository>();
        services.AddSingleton<IRecurrenceSeriesRepository, SqliteRecurrenceSeriesRepository>();
        services.AddSingleton<IReminderRepository, SqliteReminderRepository>();
        services.AddSingleton<IAttachmentRepository, SqliteAttachmentRepository>();
        services.AddSingleton<IRecurrenceExclusionRepository, SqliteRecurrenceExclusionRepository>();

        return services;
    }
}
