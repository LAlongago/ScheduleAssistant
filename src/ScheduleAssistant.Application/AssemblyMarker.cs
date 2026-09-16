using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application;

/// <summary>
/// Identifies the Application assembly and its dependency on Domain.
/// </summary>
public static class ApplicationAssemblyMarker
{
    /// <summary>
    /// Gets the Domain assembly that Application is allowed to depend on.
    /// </summary>
    public static Type DomainAssembly => typeof(DomainAssemblyMarker).Assembly;
}
