using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

internal sealed class PersistenceMappingRecurrenceSeriesRepository : IRecurrenceSeriesRepository
{
    private readonly IRecurrenceSeriesRepository _inner;

    public PersistenceMappingRecurrenceSeriesRepository(IRecurrenceSeriesRepository inner)
    {
        _inner = inner;
    }

    public Task<RecurrenceSeries?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceSeries.GetById", () => _inner.GetByIdAsync(id, cancellationToken));

    public Task<RecurrenceSeries?> GetByIdAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceSeries.GetByIdInTransaction", () => _inner.GetByIdAsync(id, transaction, cancellationToken));

    public Task<IReadOnlyList<RecurrenceSeries>> GetAllAsync(CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceSeries.GetAll", () => _inner.GetAllAsync(cancellationToken));

    public Task<PersistenceCommitResult<RecurrenceSeries>> AddAsync(RecurrenceSeries series, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceSeries.Add", () => _inner.AddAsync(series, cancellationToken));

    public Task<PersistenceCommitResult<RecurrenceSeries>> AddAsync(RecurrenceSeries series, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceSeries.AddInTransaction", () => _inner.AddAsync(series, transaction, cancellationToken));

    public Task<PersistenceCommitResult<RecurrenceSeries>> UpdateAsync(RecurrenceSeries series, long expectedVersion, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceSeries.Update", () => _inner.UpdateAsync(series, expectedVersion, cancellationToken));

    public Task<PersistenceCommitResult<RecurrenceSeries>> UpdateAsync(RecurrenceSeries series, long expectedVersion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceSeries.UpdateInTransaction", () => _inner.UpdateAsync(series, expectedVersion, transaction, cancellationToken));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceSeries.Delete", () => _inner.DeleteAsync(id, cancellationToken));

    public Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceSeries.DeleteInTransaction", () => _inner.DeleteAsync(id, transaction, cancellationToken));
}

internal sealed class PersistenceMappingRecurrenceExclusionRepository : IRecurrenceExclusionRepository
{
    private readonly IRecurrenceExclusionRepository _inner;

    public PersistenceMappingRecurrenceExclusionRepository(IRecurrenceExclusionRepository inner)
    {
        _inner = inner;
    }

    public Task<bool> ExistsAsync(Guid seriesId, DateOnly occurrenceDate, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceExclusion.Exists", () => _inner.ExistsAsync(seriesId, occurrenceDate, cancellationToken));

    public Task<IReadOnlyList<RecurrenceExclusion>> GetBySeriesAndRangeAsync(Guid seriesId, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceExclusion.GetByRange", () => _inner.GetBySeriesAndRangeAsync(seriesId, rangeStart, rangeEnd, cancellationToken));

    public Task<IReadOnlyList<RecurrenceExclusion>> GetBySeriesAndRangeAsync(Guid seriesId, DateOnly rangeStart, DateOnly rangeEnd, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceExclusion.GetByRangeInTransaction", () => _inner.GetBySeriesAndRangeAsync(seriesId, rangeStart, rangeEnd, transaction, cancellationToken));

    public Task AddAsync(RecurrenceExclusion exclusion, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceExclusion.Add", () => _inner.AddAsync(exclusion, cancellationToken));

    public Task AddAsync(RecurrenceExclusion exclusion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceExclusion.AddInTransaction", () => _inner.AddAsync(exclusion, transaction, cancellationToken));

    public Task<bool> AddIfAbsentAsync(RecurrenceExclusion exclusion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceExclusion.AddIfAbsentInTransaction", () => _inner.AddIfAbsentAsync(exclusion, transaction, cancellationToken));

    public Task DeleteAsync(Guid seriesId, DateOnly occurrenceDate, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceExclusion.Delete", () => _inner.DeleteAsync(seriesId, occurrenceDate, cancellationToken));

    public Task DeleteAsync(Guid seriesId, DateOnly occurrenceDate, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceExclusion.DeleteInTransaction", () => _inner.DeleteAsync(seriesId, occurrenceDate, transaction, cancellationToken));
}

internal sealed class PersistenceMappingRecurrenceTaskRepository : IRecurrenceTaskRepository
{
    private readonly IRecurrenceTaskRepository _inner;

    public PersistenceMappingRecurrenceTaskRepository(IRecurrenceTaskRepository inner)
    {
        _inner = inner;
    }

    public Task<PersistenceCommitResult<TaskItem>?> AddIfAbsentAsync(TaskItem item, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceTask.AddIfAbsentInTransaction", () => _inner.AddIfAbsentAsync(item, transaction, cancellationToken));

    public Task<IReadOnlyList<TaskItem>> DeleteUncompletedNonOverrideBySeriesFromDateAsync(Guid seriesId, DateOnly fromDate, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("RecurrenceTask.DeleteFutureInTransaction", () => _inner.DeleteUncompletedNonOverrideBySeriesFromDateAsync(seriesId, fromDate, transaction, cancellationToken));
}
