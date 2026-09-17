using System.Reflection;
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
    public static Assembly ApplicationAssembly => typeof(ApplicationAssemblyMarker).Assembly;

    /// <summary>
    /// Gets the Domain assembly consumed by Infrastructure adapters.
    /// </summary>
    public static Assembly DomainAssembly => typeof(DomainAssemblyMarker).Assembly;
}
