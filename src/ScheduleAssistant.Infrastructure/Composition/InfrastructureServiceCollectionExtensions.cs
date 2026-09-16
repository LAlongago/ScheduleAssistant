using Microsoft.Extensions.DependencyInjection;
using ScheduleAssistant.Application;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Composition;

/// <summary>
/// Registers the Infrastructure composition boundary.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the current Infrastructure boundary. Concrete adapters are added by their
    /// owning implementation tasks; DEV-001 intentionally registers no business services.
    /// </summary>
    /// <param name="services">The service collection owned by the application composition root.</param>
    /// <returns>The same service collection for fluent composition.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = typeof(ApplicationAssemblyMarker);
        _ = typeof(DomainAssemblyMarker);

        return services;
    }
}
