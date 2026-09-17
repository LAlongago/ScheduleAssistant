namespace ScheduleAssistant.Domain;

/// <summary>
/// Indicates that a domain value or entity state violates a business invariant.
/// </summary>
public sealed class DomainValidationException : ArgumentException
{
    /// <summary>
    /// Initializes a validation exception for a domain input.
    /// </summary>
    public DomainValidationException(string message, string? paramName = null)
        : base(message, paramName)
    {
    }
}
