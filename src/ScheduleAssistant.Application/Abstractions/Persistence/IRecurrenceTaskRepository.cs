using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>
/// Persistence operations that are specific to materialized recurrence instances.
/// The instances themselves remain ordinary <see cref="TaskItem"/> entities.
/// </summary>
public interface IRecurrenceTaskRepository
{
    /// <summary>
    /// Inserts a materialized task unless the series/date unique key already exists.
    /// A <see langword="null"/> result means another caller already materialized it.
    /// </summary>
    Task<PersistenceCommitResult<TaskItem>?> AddIfAbsentAsync(
        TaskItem item,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes unfinished, non-overridden instances from the supplied occurrence date onward.
    /// Completed and instance-level override tasks are left untouched.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> DeleteUncompletedNonOverrideBySeriesFromDateAsync(
        Guid seriesId,
        DateOnly fromDate,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);
}
