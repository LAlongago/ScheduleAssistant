namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>Reads and writes occurrence exclusions for recurrence materialization.</summary>
public interface IRecurrenceExclusionRepository
{
    /// <summary>Checks whether an occurrence is excluded.</summary>
    Task<bool> ExistsAsync(Guid seriesId, DateOnly occurrenceDate, CancellationToken cancellationToken = default);

    /// <summary>Gets exclusions for a series in an inclusive date range.</summary>
    Task<IReadOnlyList<RecurrenceExclusion>> GetBySeriesAndRangeAsync(
        Guid seriesId,
        DateOnly rangeStart,
        DateOnly rangeEnd,
        CancellationToken cancellationToken = default);

    /// <summary>Adds an occurrence exclusion.</summary>
    Task AddAsync(RecurrenceExclusion exclusion, CancellationToken cancellationToken = default);

    /// <summary>Adds an occurrence exclusion in an existing transaction.</summary>
    Task AddAsync(RecurrenceExclusion exclusion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Removes an occurrence exclusion.</summary>
    Task DeleteAsync(Guid seriesId, DateOnly occurrenceDate, CancellationToken cancellationToken = default);

    /// <summary>Removes an occurrence exclusion in an existing transaction.</summary>
    Task DeleteAsync(
        Guid seriesId,
        DateOnly occurrenceDate,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);
}
