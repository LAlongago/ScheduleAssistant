using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Recurrence;

public sealed partial class RecurrenceUseCases
{
    private async Task<ApplicationResult<RecurrenceSeriesDto>> CreateCoreAsync(
        CreateRecurrenceSeriesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Draft);
        ArgumentNullException.ThrowIfNull(command.Draft.Rule);
        cancellationToken.ThrowIfCancellationRequested();

        var draft = command.Draft;
        var series = RecurrenceSeries.Create(
            _newId(),
            draft.Title,
            draft.CategoryId,
            TaskContractMapper.ToDomain(draft.Priority),
            ToDomainRule(draft.Rule),
            _timeProvider.GetUtcNow().ToUniversalTime(),
            draft.PlannedStart,
            draft.PlannedEnd,
            draft.Location,
            draft.Description,
            draft.Materials,
            draft.Notes,
            draft.IsEnabled);

        IReadOnlyList<MaterializedRecurrenceTask> inserted;
        await using (var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            var category = await _categoryRepository
                .GetByIdAsync(series.CategoryId, transaction, cancellationToken)
                .ConfigureAwait(false);
            var categoryError = ValidateCategory(category);
            if (categoryError is not null)
            {
                return ApplicationResult<RecurrenceSeriesDto>.Failure(categoryError);
            }

            var saved = await _seriesRepository
                .AddAsync(series, transaction, cancellationToken)
                .ConfigureAwait(false);
            inserted = await MaterializeDefaultWindowAsync(saved.Entity, transaction, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            series = saved.Entity;
        }

        var events = new List<IApplicationEvent>();
        AddTaskCreatedEvents(events, inserted);
        events.Add(new RecurrenceSeriesChanged(
            series.Id,
            series.Version,
            AffectedDates(Array.Empty<TaskItem>(), inserted, series.EffectiveDate),
            RecurrenceChangeKind.Created));
        var eventStatus = await PublishAfterCommitAsync(events, cancellationToken).ConfigureAwait(false);
        return ApplicationResult<RecurrenceSeriesDto>.Success(ToDto(series), postCommitEventStatus: eventStatus);
    }

    private async Task<ApplicationResult<RecurrenceSeriesDto>> UpdateCoreAsync(
        UpdateRecurrenceSeriesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Draft);
        ArgumentNullException.ThrowIfNull(command.Draft.Rule);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<TaskItem> deleted;
        IReadOnlyList<MaterializedRecurrenceTask> inserted;
        RecurrenceSeries savedSeries;
        var applyFromDate = command.ApplyFromDate ?? command.Draft.Rule.EffectiveDate;
        await using (var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            var existing = await _seriesRepository
                .GetByIdAsync(command.SeriesId, transaction, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                return ApplicationResult<RecurrenceSeriesDto>.Failure(NotFound("RecurrenceSeries.NotFound"));
            }

            var expectedVersionError = ValidateExpectedVersion(existing, command.ExpectedVersion);
            if (expectedVersionError is not null)
            {
                return ApplicationResult<RecurrenceSeriesDto>.Failure(expectedVersionError);
            }

            if (existing.CategoryId != command.Draft.CategoryId)
            {
                var category = await _categoryRepository
                    .GetByIdAsync(command.Draft.CategoryId, transaction, cancellationToken)
                    .ConfigureAwait(false);
                var categoryError = ValidateCategory(category);
                if (categoryError is not null)
                {
                    return ApplicationResult<RecurrenceSeriesDto>.Failure(categoryError);
                }
            }

            existing.UpdateDetails(
                command.Draft.Title,
                command.Draft.CategoryId,
                TaskContractMapper.ToDomain(command.Draft.Priority),
                ToDomainRule(command.Draft.Rule, applyFromDate),
                command.Draft.PlannedStart,
                command.Draft.PlannedEnd,
                command.Draft.Location,
                command.Draft.Description,
                command.Draft.Materials,
                command.Draft.Notes,
                command.Draft.IsEnabled,
                _timeProvider.GetUtcNow().ToUniversalTime());
            var updated = await _seriesRepository
                .UpdateAsync(existing, command.ExpectedVersion, transaction, cancellationToken)
                .ConfigureAwait(false);
            savedSeries = updated.Entity;
            deleted = await _recurrenceTaskRepository
                .DeleteUncompletedNonOverrideBySeriesFromDateAsync(
                    savedSeries.Id,
                    applyFromDate,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            inserted = await MaterializeDefaultWindowAsync(savedSeries, transaction, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var events = new List<IApplicationEvent>();
        AddTaskDeletedEvents(events, deleted);
        AddTaskCreatedEvents(events, inserted);
        events.Add(new RecurrenceSeriesChanged(
            savedSeries.Id,
            savedSeries.Version,
            AffectedDates(deleted, inserted, applyFromDate),
            RecurrenceChangeKind.Updated));
        var eventStatus = await PublishAfterCommitAsync(events, cancellationToken).ConfigureAwait(false);
        return ApplicationResult<RecurrenceSeriesDto>.Success(
            ToDto(savedSeries),
            postCommitEventStatus: eventStatus);
    }

    private async Task<ApplicationResult<RecurrenceSeriesDto>> DeactivateCoreAsync(
        DeactivateRecurrenceSeriesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        RecurrenceSeries saved;
        await using (var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            var existing = await _seriesRepository
                .GetByIdAsync(command.SeriesId, transaction, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                return ApplicationResult<RecurrenceSeriesDto>.Failure(NotFound("RecurrenceSeries.NotFound"));
            }

            var expectedVersionError = ValidateExpectedVersion(existing, command.ExpectedVersion);
            if (expectedVersionError is not null)
            {
                return ApplicationResult<RecurrenceSeriesDto>.Failure(expectedVersionError);
            }

            existing.UpdateDetails(
                existing.Title,
                existing.CategoryId,
                existing.Priority,
                existing.Rule,
                existing.PlannedStart,
                existing.PlannedEnd,
                existing.Location,
                existing.Description,
                existing.Materials,
                existing.Notes,
                isEnabled: false,
                _timeProvider.GetUtcNow().ToUniversalTime());
            saved = (await _seriesRepository
                .UpdateAsync(existing, command.ExpectedVersion, transaction, cancellationToken)
                .ConfigureAwait(false)).Entity;
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var eventStatus = await PublishAfterCommitAsync(
            new IApplicationEvent[]
            {
                new RecurrenceSeriesChanged(
                    saved.Id,
                    saved.Version,
                    new[] { saved.EffectiveDate },
                    RecurrenceChangeKind.Deactivated)
            },
            cancellationToken).ConfigureAwait(false);
        return ApplicationResult<RecurrenceSeriesDto>.Success(
            ToDto(saved),
            postCommitEventStatus: eventStatus);
    }

    private async Task<ApplicationResult<DeletedRecurrenceInstanceDto>> DeleteInstanceCoreAsync(
        DeleteRecurrenceInstanceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        TaskItem existing;
        RecurrenceSeries series;
        await using (var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            existing = await _taskRepository
                .GetByIdAsync(command.TaskId, transaction, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new PersistenceNotFoundException(nameof(TaskItem), command.TaskId);
            if (existing.SeriesId is not Guid seriesId || existing.OccurrenceDate is null)
            {
                return ApplicationResult<DeletedRecurrenceInstanceDto>.Failure(
                    ApplicationErrorMapper.Validation(
                        "Recurrence.InstanceRequired",
                        "Only a materialized recurrence instance can use this operation."));
            }

            var expectedVersionError = existing.Version == command.ExpectedVersion
                ? null
                : new ApplicationError(
                    ApplicationErrorKind.Conflict,
                    "Persistence.Conflict",
                    "The item was changed elsewhere. Reload it and try again.");
            if (expectedVersionError is not null)
            {
                return ApplicationResult<DeletedRecurrenceInstanceDto>.Failure(expectedVersionError);
            }

            series = await _seriesRepository
                .GetByIdAsync(seriesId, transaction, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new PersistenceNotFoundException(nameof(RecurrenceSeries), seriesId);
            await _exclusionRepository
                .AddIfAbsentAsync(
                    new RecurrenceExclusion(seriesId, existing.OccurrenceDate.Date),
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            _ = await _taskRepository
                .DeleteAsync(existing.Id, command.ExpectedVersion, transaction, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var affectedDates = AffectedDates(existing.OccurrenceDate!.Date);
        var eventStatus = await PublishAfterCommitAsync(
            new IApplicationEvent[]
            {
                new TaskDeleted(existing.Id, existing.Version, affectedDates),
                new RecurrenceSeriesChanged(
                    series.Id,
                    series.Version,
                    affectedDates,
                    RecurrenceChangeKind.OccurrenceDeleted)
            },
            cancellationToken).ConfigureAwait(false);
        return ApplicationResult<DeletedRecurrenceInstanceDto>.Success(
            new DeletedRecurrenceInstanceDto(existing.Id, series.Id, existing.OccurrenceDate.Date, affectedDates),
            postCommitEventStatus: eventStatus);
    }

    private async Task<ApplicationResult<DeletedFutureRecurrenceDto>> DeleteFutureCoreAsync(
        DeleteFutureRecurrenceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        RecurrenceSeries saved;
        IReadOnlyList<TaskItem> deleted;
        await using (var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            var existing = await _seriesRepository
                .GetByIdAsync(command.SeriesId, transaction, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                return ApplicationResult<DeletedFutureRecurrenceDto>.Failure(NotFound("RecurrenceSeries.NotFound"));
            }

            var expectedVersionError = ValidateExpectedVersion(existing, command.ExpectedVersion);
            if (expectedVersionError is not null)
            {
                return ApplicationResult<DeletedFutureRecurrenceDto>.Failure(expectedVersionError);
            }

            var disablesSeries = command.FromDate <= existing.EffectiveDate;
            var rule = disablesSeries
                ? existing.Rule
                : ToDomainRule(existing.Rule, EarlierEndDate(existing.Rule.EndDate, command.FromDate.AddDays(-1)));
            existing.UpdateDetails(
                existing.Title,
                existing.CategoryId,
                existing.Priority,
                rule,
                existing.PlannedStart,
                existing.PlannedEnd,
                existing.Location,
                existing.Description,
                existing.Materials,
                existing.Notes,
                isEnabled: disablesSeries ? false : existing.IsEnabled,
                _timeProvider.GetUtcNow().ToUniversalTime());
            saved = (await _seriesRepository
                .UpdateAsync(existing, command.ExpectedVersion, transaction, cancellationToken)
                .ConfigureAwait(false)).Entity;
            deleted = await _recurrenceTaskRepository
                .DeleteUncompletedNonOverrideBySeriesFromDateAsync(
                    command.SeriesId,
                    command.FromDate,
                    transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var affectedDates = AffectedDates(deleted, Array.Empty<MaterializedRecurrenceTask>(), command.FromDate);
        var events = new List<IApplicationEvent>();
        AddTaskDeletedEvents(events, deleted);
        events.Add(new RecurrenceSeriesChanged(
            saved.Id,
            saved.Version,
            affectedDates,
            RecurrenceChangeKind.FutureDeleted));
        var eventStatus = await PublishAfterCommitAsync(events, cancellationToken).ConfigureAwait(false);
        return ApplicationResult<DeletedFutureRecurrenceDto>.Success(
            new DeletedFutureRecurrenceDto(
                saved.Id,
                command.FromDate,
                deleted.Select(task => task.Id).ToArray(),
                saved.Version,
                affectedDates),
            postCommitEventStatus: eventStatus);
    }

    private async Task<IReadOnlyList<MaterializedRecurrenceTask>> MaterializeDefaultWindowAsync(
        RecurrenceSeries series,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (_materializer is null || !series.IsEnabled)
        {
            return Array.Empty<MaterializedRecurrenceTask>();
        }

        var window = RecurrenceMaterializationWindow.CreateDefault(_timeProvider);
        return await _materializer
            .MaterializeSeriesAsync(series, window.FromDate, window.ToDate, transaction, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void AddTaskCreatedEvents(
        List<IApplicationEvent> events,
        IEnumerable<MaterializedRecurrenceTask> inserted)
    {
        foreach (var task in inserted)
        {
            events.Add(new TaskCreated(task.TaskId, task.Version, new[] { task.OccurrenceDate }));
        }
    }

    private static void AddTaskDeletedEvents(
        List<IApplicationEvent> events,
        IEnumerable<TaskItem> deleted)
    {
        foreach (var task in deleted)
        {
            var affectedDates = task.OccurrenceDate is null
                ? task.PlannedDate.HasValue ? new[] { task.PlannedDate.Value } : Array.Empty<DateOnly>()
                : new[] { task.OccurrenceDate.Date };
            events.Add(new TaskDeleted(task.Id, task.Version, affectedDates));
        }
    }

    private static DateOnly? EarlierEndDate(DateOnly? existingEndDate, DateOnly candidate)
    {
        return existingEndDate.HasValue && existingEndDate.Value <= candidate
            ? existingEndDate
            : candidate;
    }
}
