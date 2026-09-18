namespace ScheduleAssistant.Application.Common;

/// <summary>Classifies an error that can be presented by an Application use case.</summary>
public enum ApplicationErrorKind
{
    /// <summary>The request or a domain invariant is invalid.</summary>
    Validation,

    /// <summary>The requested entity or reference does not exist.</summary>
    NotFound,

    /// <summary>The request was based on a stale persistence version.</summary>
    Conflict,

    /// <summary>The local persistence store is unavailable.</summary>
    StorageUnavailable,

    /// <summary>The operation failed for an unclassified reason.</summary>
    Unexpected
}

/// <summary>A safe, provider-independent error returned by an Application use case.</summary>
public sealed record ApplicationError(
    ApplicationErrorKind Kind,
    string Code,
    string Message);

/// <summary>A non-blocking warning returned with an otherwise successful result.</summary>
public sealed record ApplicationWarning(string Code, string Message);

/// <summary>Describes whether post-commit event delivery completed.</summary>
public enum PostCommitEventStatus
{
    /// <summary>No event delivery was requested, normally for a read or an idempotent no-op.</summary>
    NotAttempted,

    /// <summary>All events were delivered to the in-process publisher.</summary>
    Published,

    /// <summary>The database commit succeeded but a refresh signal is required.</summary>
    RefreshRequired
}

// Generic factories keep call sites type-safe and avoid a second non-generic factory type.
#pragma warning disable CA1000
/// <summary>Result returned by an Application use case.</summary>
/// <typeparam name="T">The use case value type.</typeparam>
public sealed class ApplicationResult<T>
{
    private ApplicationResult(
        bool isSuccess,
        T? value,
        ApplicationError? error,
        IReadOnlyList<ApplicationWarning> warnings,
        PostCommitEventStatus postCommitEventStatus)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        Warnings = warnings;
        PostCommitEventStatus = postCommitEventStatus;
    }

    /// <summary>Gets whether the requested operation committed successfully.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the returned value when <see cref="IsSuccess"/> is true.</summary>
    public T? Value { get; }

    /// <summary>Gets the safe error when <see cref="IsSuccess"/> is false.</summary>
    public ApplicationError? Error { get; }

    /// <summary>Gets non-blocking warnings associated with a successful result.</summary>
    public IReadOnlyList<ApplicationWarning> Warnings { get; }

    /// <summary>Gets the outcome of post-commit event delivery.</summary>
    public PostCommitEventStatus PostCommitEventStatus { get; }

    /// <summary>Gets whether consumers should perform a lightweight refresh.</summary>
    public bool RequiresRefresh => PostCommitEventStatus == PostCommitEventStatus.RefreshRequired;

    /// <summary>Creates a successful result.</summary>
    public static ApplicationResult<T> Success(
        T value,
        IEnumerable<ApplicationWarning>? warnings = null,
        PostCommitEventStatus postCommitEventStatus = PostCommitEventStatus.NotAttempted)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApplicationResult<T>(
            true,
            value,
            error: null,
            ToReadOnlyWarnings(warnings),
            postCommitEventStatus);
    }

    /// <summary>Creates a failed result without exposing an infrastructure exception.</summary>
    public static ApplicationResult<T> Failure(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApplicationResult<T>(
            false,
            value: default,
            error,
            Array.Empty<ApplicationWarning>(),
            PostCommitEventStatus.NotAttempted);
    }

    private static ApplicationWarning[] ToReadOnlyWarnings(
        IEnumerable<ApplicationWarning>? warnings)
    {
        return warnings is null
            ? Array.Empty<ApplicationWarning>()
            : warnings.ToArray();
    }
}
#pragma warning restore CA1000
