namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>Classifies a provider-independent persistence failure.</summary>
public enum PersistenceFailureKind
{
    /// <summary>The store cannot currently be read or written.</summary>
    Unavailable,

    /// <summary>The store rejected a validly shaped operation because of stored data.</summary>
    Constraint,

    /// <summary>The provider reported a failure with no safe narrower classification.</summary>
    Unknown
}

/// <summary>
/// Provider-neutral persistence failure used at the Application boundary.
/// The original exception remains internal to diagnostics and is never exposed in a result.
/// </summary>
public sealed class PersistenceFailureException : Exception
{
    /// <summary>Initializes a persistence failure.</summary>
    public PersistenceFailureException(
        PersistenceFailureKind kind,
        string operation,
        Exception? innerException = null)
        : base("A persistence operation failed.", innerException)
    {
        Kind = kind;
        Operation = string.IsNullOrWhiteSpace(operation) ? "Unknown" : operation;
    }

    /// <summary>Gets the provider-independent failure kind.</summary>
    public PersistenceFailureKind Kind { get; }

    /// <summary>Gets a non-sensitive logical operation name.</summary>
    public string Operation { get; }
}
