using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Common;

namespace ScheduleAssistant.Application.Recurrence;

/// <summary>
/// Orchestrates recurrence series lifecycle, transactional instance rebuilding, and
/// post-commit refresh events.
/// </summary>
public sealed partial class RecurrenceUseCases : IRecurrenceUseCases
{
    private readonly IRecurrenceSeriesRepository _seriesRepository;
    private readonly IRecurrenceExclusionRepository _exclusionRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IRecurrenceTaskRepository _recurrenceTaskRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPersistenceTransactionFactory _transactionFactory;
    private readonly ITransactionalRecurrenceMaterializer? _materializer;
    private readonly IApplicationEventPublisher _eventPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly Func<Guid> _newId;

    /// <summary>Initializes recurrence series use cases.</summary>
    public RecurrenceUseCases(
        IRecurrenceSeriesRepository seriesRepository,
        IRecurrenceExclusionRepository exclusionRepository,
        ITaskRepository taskRepository,
        IRecurrenceTaskRepository recurrenceTaskRepository,
        ICategoryRepository categoryRepository,
        IPersistenceTransactionFactory transactionFactory,
        ITransactionalRecurrenceMaterializer? materializer = null,
        IApplicationEventPublisher? eventPublisher = null,
        TimeProvider? timeProvider = null,
        Func<Guid>? idFactory = null)
    {
        _seriesRepository = seriesRepository ?? throw new ArgumentNullException(nameof(seriesRepository));
        _exclusionRepository = exclusionRepository ?? throw new ArgumentNullException(nameof(exclusionRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _recurrenceTaskRepository = recurrenceTaskRepository ?? throw new ArgumentNullException(nameof(recurrenceTaskRepository));
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
        _materializer = materializer;
        _eventPublisher = eventPublisher ?? new InProcessEventBus();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _newId = idFactory ?? Guid.NewGuid;
    }

    /// <inheritdoc />
    public Task<ApplicationResult<RecurrenceSeriesDto>> CreateAsync(
        CreateRecurrenceSeriesCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => CreateCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<RecurrenceSeriesDto>> GetAsync(
        GetRecurrenceSeriesQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var series = await _seriesRepository.GetByIdAsync(query.SeriesId, cancellationToken).ConfigureAwait(false);
            return series is null
                ? ApplicationResult<RecurrenceSeriesDto>.Failure(NotFound("RecurrenceSeries.NotFound"))
                : ApplicationResult<RecurrenceSeriesDto>.Success(ToDto(series));
        });
    }

    /// <inheritdoc />
    public Task<ApplicationResult<IReadOnlyList<RecurrenceSeriesDto>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var series = await _seriesRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            return ApplicationResult<IReadOnlyList<RecurrenceSeriesDto>>.Success(
                series.Select(ToDto).ToArray());
        });
    }

    /// <inheritdoc />
    public Task<ApplicationResult<RecurrenceSeriesDto>> UpdateAsync(
        UpdateRecurrenceSeriesCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => UpdateCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<RecurrenceSeriesDto>> DeactivateAsync(
        DeactivateRecurrenceSeriesCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => DeactivateCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<DeletedRecurrenceInstanceDto>> DeleteInstanceAsync(
        DeleteRecurrenceInstanceCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => DeleteInstanceCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<DeletedFutureRecurrenceDto>> DeleteFutureAsync(
        DeleteFutureRecurrenceCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => DeleteFutureCoreAsync(command, cancellationToken));
    }

    private async Task<PostCommitEventStatus> PublishAfterCommitAsync(
        IEnumerable<IApplicationEvent> events,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var applicationEvent in events)
            {
                await _eventPublisher.PublishAsync(applicationEvent, cancellationToken).ConfigureAwait(false);
            }

            return PostCommitEventStatus.Published;
        }
        catch (OperationCanceledException)
        {
            return PostCommitEventStatus.RefreshRequired;
        }
        catch (Exception)
        {
            return PostCommitEventStatus.RefreshRequired;
        }
    }

    private static async Task<ApplicationResult<T>> ExecuteAsync<T>(Func<Task<ApplicationResult<T>>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ApplicationResult<T>.Failure(ApplicationErrorMapper.Map(exception));
        }
    }
}
