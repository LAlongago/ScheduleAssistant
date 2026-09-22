using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Recurrence;

/// <summary>
/// Materializes recurrence definitions as ordinary tasks. The database unique key is the final
/// idempotency boundary, while the gate avoids unnecessary local contention.
/// </summary>
public sealed class RecurrenceMaterializer : ITransactionalRecurrenceMaterializer, IDisposable
{
    private readonly IRecurrenceSeriesRepository _seriesRepository;
    private readonly IRecurrenceExclusionRepository _exclusionRepository;
    private readonly IRecurrenceTaskRepository _taskRepository;
    private readonly IPersistenceTransactionFactory _transactionFactory;
    private readonly IApplicationEventPublisher _eventPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly Func<Guid> _newId;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes the recurrence materializer.</summary>
    public RecurrenceMaterializer(
        IRecurrenceSeriesRepository seriesRepository,
        IRecurrenceExclusionRepository exclusionRepository,
        IRecurrenceTaskRepository taskRepository,
        IPersistenceTransactionFactory transactionFactory,
        IApplicationEventPublisher? eventPublisher = null,
        TimeProvider? timeProvider = null,
        Func<Guid>? idFactory = null)
    {
        _seriesRepository = seriesRepository ?? throw new ArgumentNullException(nameof(seriesRepository));
        _exclusionRepository = exclusionRepository ?? throw new ArgumentNullException(nameof(exclusionRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
        _eventPublisher = eventPublisher ?? new InProcessEventBus();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _newId = idFactory ?? Guid.NewGuid;
    }

    /// <inheritdoc />
    public async Task MaterializeAsync(
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var window = ResolveWindow(fromDate, toDate);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var series = await _seriesRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            foreach (var candidate in series.Where(series => series.IsEnabled))
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<MaterializedRecurrenceTask> inserted;
                RecurrenceSeries? currentSeries;
                await using (var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false))
                {
                    currentSeries = await _seriesRepository
                        .GetByIdAsync(candidate.Id, transaction, cancellationToken)
                        .ConfigureAwait(false);
                    if (currentSeries is null || !currentSeries.IsEnabled)
                    {
                        continue;
                    }

                    inserted = await MaterializeSeriesCoreAsync(
                        currentSeries,
                        window.FromDate,
                        window.ToDate,
                        transaction,
                        cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                await PublishMaterializationEventsAsync(currentSeries, inserted, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaterializedRecurrenceTask>> MaterializeSeriesAsync(
        RecurrenceSeries series,
        DateOnly fromDate,
        DateOnly toDate,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(transaction);
        _ = new RecurrenceMaterializationWindow(fromDate, toDate);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await MaterializeSeriesCoreAsync(
                series,
                fromDate,
                toDate,
                transaction,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<MaterializedRecurrenceTask>> MaterializeSeriesCoreAsync(
        RecurrenceSeries series,
        DateOnly fromDate,
        DateOnly toDate,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (!series.IsEnabled)
        {
            return Array.Empty<MaterializedRecurrenceTask>();
        }

        var occurrenceDates = OccurrenceDateCalculator.Calculate(series.Rule, fromDate, toDate);
        if (occurrenceDates.Count == 0)
        {
            return Array.Empty<MaterializedRecurrenceTask>();
        }

        var exclusions = (await _exclusionRepository
                .GetBySeriesAndRangeAsync(series.Id, fromDate, toDate, transaction, cancellationToken)
                .ConfigureAwait(false))
            .Select(exclusion => exclusion.OccurrenceDate)
            .ToHashSet();
        var createdAtUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        var inserted = new List<MaterializedRecurrenceTask>();
        foreach (var occurrenceDate in occurrenceDates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (exclusions.Contains(occurrenceDate))
            {
                continue;
            }

            var task = TaskItem.Create(
                _newId(),
                series.Title,
                series.CategoryId,
                series.Priority,
                createdAtUtc,
                plannedDate: occurrenceDate,
                plannedStart: series.PlannedStart,
                plannedEnd: series.PlannedEnd,
                deadline: null,
                location: series.Location,
                description: series.Description,
                materials: series.Materials,
                notes: series.Notes,
                seriesId: series.Id,
                occurrenceDate: new OccurrenceDate(occurrenceDate));
            var saved = await _taskRepository
                .AddIfAbsentAsync(task, transaction, cancellationToken)
                .ConfigureAwait(false);
            if (saved is not null)
            {
                inserted.Add(new MaterializedRecurrenceTask(
                    saved.Entity.Id,
                    series.Id,
                    occurrenceDate,
                    saved.NewVersion));
            }
        }

        return inserted;
    }

    private async Task PublishMaterializationEventsAsync(
        RecurrenceSeries? series,
        IReadOnlyList<MaterializedRecurrenceTask> inserted,
        CancellationToken cancellationToken)
    {
        if (series is null || inserted.Count == 0)
        {
            return;
        }

        foreach (var task in inserted)
        {
            await _eventPublisher.PublishAsync(
                new TaskCreated(task.TaskId, task.Version, new[] { task.OccurrenceDate }),
                cancellationToken).ConfigureAwait(false);
        }

        await _eventPublisher.PublishAsync(
            new RecurrenceSeriesChanged(
                series.Id,
                series.Version,
                inserted.Select(task => task.OccurrenceDate).Distinct().OrderBy(date => date).ToArray(),
                RecurrenceChangeKind.Materialized),
            cancellationToken).ConfigureAwait(false);
    }

    private RecurrenceMaterializationWindow ResolveWindow(DateOnly? fromDate, DateOnly? toDate)
    {
        var defaults = RecurrenceMaterializationWindow.CreateDefault(_timeProvider);
        return new RecurrenceMaterializationWindow(
            fromDate ?? defaults.FromDate,
            toDate ?? defaults.ToDate);
    }

    /// <summary>Releases the process-local materialization gate.</summary>
    public void Dispose()
    {
        _gate.Dispose();
    }
}
