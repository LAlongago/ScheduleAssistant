using ScheduleAssistant.Application;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure;

/// <summary>
/// Identifies the Infrastructure assembly and its inward project dependencies.
/// </summary>
public static class InfrastructureAssemblyMarker
{
    /// <summary>
    /// Gets the Application assembly consumed by Infrastructure adapters.
    /// </summary>
    public static Type ApplicationAssembly => typeof(ApplicationAssemblyMarker).Assembly;

    /// <summary>
    /// Gets the Domain assembly consumed by Infrastructure adapters.
    /// </summary>
    public static Type DomainAssembly => typeof(DomainAssemblyMarker).Assembly;
}
