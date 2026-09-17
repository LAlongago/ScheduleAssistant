using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>Persists reminder nodes and their delivery state.</summary>
public interface IReminderRepository
{
    /// <summary>Finds a reminder by identity.</summary>
    Task<Reminder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets all reminders belonging to a task.</summary>
    Task<IReadOnlyList<Reminder>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>Gets the earliest pending reminder, if one exists.</summary>
    Task<Reminder?> GetNextPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Inserts a reminder.</summary>
    Task AddAsync(Reminder reminder, CancellationToken cancellationToken = default);

    /// <summary>Inserts a reminder in an existing transaction.</summary>
    Task AddAsync(Reminder reminder, IPersistenceTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Persists the current reminder state.</summary>
    Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default);

    /// <summary>Persists the current reminder state in an existing transaction.</summary>
    Task UpdateAsync(Reminder reminder, IPersistenceTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Deletes a reminder.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a reminder in an existing transaction.</summary>
    Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default);
}
