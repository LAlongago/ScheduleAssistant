using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>Persists recurrence series without materializing their instances.</summary>
public interface IRecurrenceSeriesRepository
{
    /// <summary>Finds a recurrence series by identity.</summary>
    Task<RecurrenceSeries?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets enabled or disabled recurrence series in identity order.</summary>
    Task<IReadOnlyList<RecurrenceSeries>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Inserts a recurrence series.</summary>
    Task<PersistenceCommitResult<RecurrenceSeries>> AddAsync(
        RecurrenceSeries series,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts a recurrence series in an existing transaction.</summary>
    Task<PersistenceCommitResult<RecurrenceSeries>> AddAsync(
        RecurrenceSeries series,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Updates a series only when its stored version equals the expected version.</summary>
    Task<PersistenceCommitResult<RecurrenceSeries>> UpdateAsync(
        RecurrenceSeries series,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>Updates a series in an existing transaction.</summary>
    Task<PersistenceCommitResult<RecurrenceSeries>> UpdateAsync(
        RecurrenceSeries series,
        long expectedVersion,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a series; referenced instances are protected by a foreign key.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a series in an existing transaction.</summary>
    Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default);
}
