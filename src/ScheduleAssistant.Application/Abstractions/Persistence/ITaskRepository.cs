using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>Persists ordinary tasks and materialized recurrence instances.</summary>
public interface ITaskRepository
{
    /// <summary>Finds a task by identity.</summary>
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reads a task through a caller-owned transaction.</summary>
    Task<TaskItem?> GetByIdAsync(
        Guid id,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Gets tasks planned on the supplied local date.</summary>
    Task<IReadOnlyList<TaskItem>> GetPlannedByDateAsync(DateOnly plannedOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tasks with a planned date or deadline local date in the inclusive range.
    /// Deadline-only tasks are included.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetByRangeAsync(DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets uncompleted tasks that need attention today: historical plans, today's plans,
    /// today's local-date deadlines, or deadlines already overdue by UTC.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetTodayPendingAsync(
        DateOnly todayLocal,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Gets incomplete tasks whose UTC deadline is in the requested range.</summary>
    Task<IReadOnlyList<TaskItem>> GetUpcomingDeadlinesAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset? untilUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets incomplete tasks with deadlines in the future range and, when requested, all
    /// deadlines already overdue. A deadline exactly at <paramref name="nowUtc"/> is upcoming.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetDeadlinesAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset? untilUtc,
        bool includeOverdue,
        CancellationToken cancellationToken = default);

    /// <summary>Searches tasks with provider-neutral filters and database-side paging.</summary>
    Task<IReadOnlyList<TaskItem>> SearchAsync(
        TaskSearchFilter filter,
        long offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Counts tasks using exactly the same filters as <see cref="SearchAsync"/>.</summary>
    Task<long> CountSearchAsync(
        TaskSearchFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts a task using its current domain version.</summary>
    Task<PersistenceCommitResult<TaskItem>> AddAsync(TaskItem item, CancellationToken cancellationToken = default);

    /// <summary>Inserts a task in an existing transaction.</summary>
    Task<PersistenceCommitResult<TaskItem>> AddAsync(
        TaskItem item,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Updates a task only when its stored version equals the expected version.</summary>
    Task<PersistenceCommitResult<TaskItem>> UpdateAsync(
        TaskItem item,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>Updates a task in an existing transaction with an optimistic version check.</summary>
    Task<PersistenceCommitResult<TaskItem>> UpdateAsync(
        TaskItem item,
        long expectedVersion,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a task; dependent reminders and attachments use database cascade rules.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a task in an existing transaction.</summary>
    Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a task only when its stored version matches the expected version.
    /// </summary>
    Task<bool> DeleteAsync(
        Guid id,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>Conditionally deletes a task in a caller-owned transaction.</summary>
    Task<bool> DeleteAsync(
        Guid id,
        long expectedVersion,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);
}
