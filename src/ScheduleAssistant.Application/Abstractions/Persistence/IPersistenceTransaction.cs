namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>
/// An application-facing transaction that can be shared by multiple repositories.
/// The concrete database transaction remains an Infrastructure concern.
/// </summary>
public interface IPersistenceTransaction : IAsyncDisposable
{
    /// <summary>Commits all changes made through this transaction.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back all changes made through this transaction.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
