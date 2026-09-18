using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Tasks;

/// <summary>
/// Orchestrates ordinary task commands, basic task queries, reminder metadata, transactions,
/// and post-commit local refresh events.
/// </summary>
public sealed partial class TaskUseCases : ITaskUseCases
{
    private const int DefaultReminderOffsetMinutes = -1_440;

    private readonly ITaskRepository _taskRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IReminderRepository _reminderRepository;
    private readonly IPersistenceTransactionFactory _transactionFactory;
    private readonly IApplicationEventPublisher _eventPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly TaskDeadlineResolver _deadlineResolver;
    private readonly Func<Guid> _newId;

    /// <summary>Initializes the task use cases.</summary>
    public TaskUseCases(
        ITaskRepository taskRepository,
        ICategoryRepository categoryRepository,
        IReminderRepository reminderRepository,
        IPersistenceTransactionFactory transactionFactory,
        IApplicationEventPublisher? eventPublisher = null,
        TimeProvider? timeProvider = null,
        TaskDeadlineResolver? deadlineResolver = null,
        Func<Guid>? idFactory = null)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _reminderRepository = reminderRepository ?? throw new ArgumentNullException(nameof(reminderRepository));
        _transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
        _eventPublisher = eventPublisher ?? new InProcessEventBus();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _deadlineResolver = deadlineResolver ?? new TaskDeadlineResolver();
        _newId = idFactory ?? Guid.NewGuid;
    }

    /// <inheritdoc />
    public Task<ApplicationResult<TaskDto>> CreateAsync(
        CreateTaskCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => CreateCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<TaskDto>> UpdateAsync(
        UpdateTaskCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => UpdateCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<TaskDto>> CompleteAsync(
        ChangeTaskStateCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => CompleteCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<TaskDto>> CancelCompletionAsync(
        ChangeTaskStateCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => CancelCompletionCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<TaskDto>> StartProcessingAsync(
        ChangeTaskStateCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => StartProcessingCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<DeletedTaskDto>> DeleteAsync(
        DeleteTaskCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => DeleteCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<TaskDto>> GetAsync(
        GetTaskQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var task = await _taskRepository.GetByIdAsync(query.TaskId, cancellationToken).ConfigureAwait(false);
            return task is null
                ? ApplicationResult<TaskDto>.Failure(NotFound("Task.NotFound", "The requested task could not be found."))
                : ApplicationResult<TaskDto>.Success(ToDto(task));
        });
    }

    /// <inheritdoc />
    public Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetPlannedByDateAsync(
        GetTasksByPlannedDateQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var tasks = await _taskRepository
                .GetPlannedByDateAsync(query.PlannedDate, cancellationToken)
                .ConfigureAwait(false);
            return ApplicationResult<IReadOnlyList<TaskDto>>.Success(tasks.Select(ToDto).ToArray());
        });
    }

    /// <inheritdoc />
    public Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetUpcomingDeadlinesAsync(
        GetUpcomingDeadlinesQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var nowUtc = _timeProvider.GetUtcNow().ToUniversalTime();
            if (query.UntilUtc.HasValue && query.UntilUtc.Value.ToUniversalTime() < nowUtc)
            {
                return ApplicationResult<IReadOnlyList<TaskDto>>.Failure(
                    ApplicationErrorMapper.Validation(
                        "Validation.DeadlineRange",
                        "The deadline range end must not be earlier than the current time."));
            }

            var tasks = await _taskRepository
                .GetUpcomingDeadlinesAsync(nowUtc, query.UntilUtc, cancellationToken)
                .ConfigureAwait(false);
            return ApplicationResult<IReadOnlyList<TaskDto>>.Success(tasks.Select(ToDto).ToArray());
        });
    }

    /// <inheritdoc />
    public Task<ApplicationResult<IReadOnlyList<CategoryOptionDto>>> GetCategoryOptionsAsync(
        GetCategoryOptionsQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var categories = await _categoryRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var options = categories
                .Where(category => !category.IsArchived)
                .Select(category => new CategoryOptionDto(
                    category.Id,
                    category.Name,
                    category.ColorHex,
                    category.SortOrder))
                .ToArray();
            return ApplicationResult<IReadOnlyList<CategoryOptionDto>>.Success(options);
        });
    }

    private async Task<ApplicationResult<TaskDto>> CreateCoreAsync(
        CreateTaskCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Draft);
        cancellationToken.ThrowIfCancellationRequested();
        var deadline = ResolveDeadline(command.Draft.Deadline);
        if (deadline.Error is not null)
        {
            return ApplicationResult<TaskDto>.Failure(deadline.Error);
        }

        var createdAtUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        var task = TaskItem.Create(
            _newId(),
            command.Draft.Title,
            command.Draft.CategoryId,
            command.Draft.Priority,
            createdAtUtc,
            command.Draft.PlannedDate,
            command.Draft.PlannedStart,
            command.Draft.PlannedEnd,
            deadline.Deadline,
            command.Draft.Location,
            command.Draft.Description,
            command.Draft.Materials,
            command.Draft.Notes);

        await using var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false);
        var category = await _categoryRepository
            .GetByIdAsync(task.CategoryId, transaction, cancellationToken)
            .ConfigureAwait(false);
        var categoryError = ValidateCategory(category);
        if (categoryError is not null)
        {
            return ApplicationResult<TaskDto>.Failure(categoryError);
        }

        var saved = await _taskRepository.AddAsync(task, transaction, cancellationToken).ConfigureAwait(false);
        var reminderCreated = await CreateReminderIfApplicableAsync(
            saved.Entity,
            command.Draft.ReminderPlan ?? new ReminderPlanInput(
                Enabled: true,
                RelativeOffsetMinutes: DefaultReminderOffsetMinutes),
            transaction,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var events = new List<IApplicationEvent>
        {
            new TaskCreated(saved.Entity.Id, saved.NewVersion, AffectedDates(saved.Entity, saved.Entity))
        };
        if (reminderCreated)
        {
            events.Add(new ReminderPlanChanged(
                saved.Entity.Id,
                saved.NewVersion,
                AffectedDates(saved.Entity, saved.Entity),
                HasPendingPlan: true));
        }

        var eventStatus = await PublishAfterCommitAsync(events, cancellationToken).ConfigureAwait(false);
        return ApplicationResult<TaskDto>.Success(
            ToDto(saved.Entity),
            WarningsFor(saved.Entity),
            eventStatus);
    }

    private async Task<ApplicationResult<TaskDto>> UpdateCoreAsync(
        UpdateTaskCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Draft);
        cancellationToken.ThrowIfCancellationRequested();
        var deadline = ResolveDeadline(command.Draft.Deadline);
        if (deadline.Error is not null)
        {
            return ApplicationResult<TaskDto>.Failure(deadline.Error);
        }

        await using var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false);
        var existing = await _taskRepository
            .GetByIdAsync(command.TaskId, transaction, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return ApplicationResult<TaskDto>.Failure(NotFound("Task.NotFound", "The requested task could not be found."));
        }

        var expectedVersionError = ValidateExpectedVersion(existing, command.ExpectedVersion);
        if (expectedVersionError is not null)
        {
            return ApplicationResult<TaskDto>.Failure(expectedVersionError);
        }

        if (existing.SeriesId.HasValue)
        {
            return ApplicationResult<TaskDto>.Failure(ApplicationErrorMapper.Validation(
                "Task.RecurrenceEditDeferred",
                "Editing a recurrence instance is handled by the recurrence use case."));
        }

        if (existing.CategoryId != command.Draft.CategoryId)
        {
            var category = await _categoryRepository
                .GetByIdAsync(command.Draft.CategoryId, transaction, cancellationToken)
                .ConfigureAwait(false);
            var categoryError = ValidateCategory(category);
            if (categoryError is not null)
            {
                return ApplicationResult<TaskDto>.Failure(categoryError);
            }
        }

        var previousDeadline = existing.Deadline;
        var previousDates = AffectedDates(existing, existing);
        var deadlineChanged = previousDeadline != deadline.Deadline;
        var updatedAtUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        existing.UpdateDetails(
            command.Draft.Title,
            command.Draft.CategoryId,
            command.Draft.Priority,
            command.Draft.PlannedDate,
            command.Draft.PlannedStart,
            command.Draft.PlannedEnd,
            deadline.Deadline,
            command.Draft.Location,
            command.Draft.Description,
            command.Draft.Materials,
            command.Draft.Notes,
            updatedAtUtc);

        var saved = await _taskRepository
            .UpdateAsync(existing, command.ExpectedVersion, transaction, cancellationToken)
            .ConfigureAwait(false);
        var reminderChange = await MaintainUpdatedRemindersAsync(
            saved.Entity,
            previousDeadline,
            deadlineChanged,
            command.Draft.ReminderPlan,
            transaction,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var affectedDates = AffectedDatesFromValues(previousDates, saved.Entity);
        var events = new List<IApplicationEvent>
        {
            new TaskUpdated(saved.Entity.Id, saved.NewVersion, affectedDates, deadlineChanged)
        };
        if (reminderChange.Changed)
        {
            events.Add(new ReminderPlanChanged(
                saved.Entity.Id,
                saved.NewVersion,
                affectedDates,
                reminderChange.HasPendingPlan));
        }

        var eventStatus = await PublishAfterCommitAsync(events, cancellationToken).ConfigureAwait(false);
        return ApplicationResult<TaskDto>.Success(ToDto(saved.Entity), WarningsFor(saved.Entity), eventStatus);
    }

    private async Task<ApplicationResult<TaskDto>> CompleteCoreAsync(
        ChangeTaskStateCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false);
        var existing = await _taskRepository.GetByIdAsync(command.TaskId, transaction, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return ApplicationResult<TaskDto>.Failure(NotFound("Task.NotFound", "The requested task could not be found."));
        }

        var expectedVersionError = ValidateExpectedVersion(existing, command.ExpectedVersion);
        if (expectedVersionError is not null)
        {
            return ApplicationResult<TaskDto>.Failure(expectedVersionError);
        }

        if (existing.WorkflowStatus == WorkflowStatus.Completed)
        {
            return ApplicationResult<TaskDto>.Success(ToDto(existing));
        }

        existing.Complete(_timeProvider.GetUtcNow().ToUniversalTime());
        var saved = await _taskRepository
            .UpdateAsync(existing, command.ExpectedVersion, transaction, cancellationToken)
            .ConfigureAwait(false);
        var cancelledCount = await _reminderRepository
            .CancelPendingByTaskIdAsync(saved.Entity.Id, transaction, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var affectedDates = AffectedDates(existing, saved.Entity);
        var events = new List<IApplicationEvent>
        {
            new TaskCompletedChanged(saved.Entity.Id, saved.NewVersion, affectedDates, IsCompleted: true)
        };
        if (cancelledCount > 0 || saved.Entity.Deadline is not null)
        {
            events.Add(new ReminderPlanChanged(
                saved.Entity.Id,
                saved.NewVersion,
                affectedDates,
                HasPendingPlan: false));
        }

        var eventStatus = await PublishAfterCommitAsync(events, cancellationToken).ConfigureAwait(false);
        return ApplicationResult<TaskDto>.Success(ToDto(saved.Entity), WarningsFor(saved.Entity), eventStatus);
    }

    private async Task<ApplicationResult<TaskDto>> CancelCompletionCoreAsync(
        ChangeTaskStateCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false);
        var existing = await _taskRepository.GetByIdAsync(command.TaskId, transaction, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return ApplicationResult<TaskDto>.Failure(NotFound("Task.NotFound", "The requested task could not be found."));
        }

        var expectedVersionError = ValidateExpectedVersion(existing, command.ExpectedVersion);
        if (expectedVersionError is not null)
        {
            return ApplicationResult<TaskDto>.Failure(expectedVersionError);
        }

        if (existing.WorkflowStatus != WorkflowStatus.Completed)
        {
            return ApplicationResult<TaskDto>.Success(ToDto(existing));
        }

        var reminders = await _reminderRepository
            .GetByTaskIdAsync(existing.Id, transaction, cancellationToken)
            .ConfigureAwait(false);
        existing.CancelCompletion(_timeProvider.GetUtcNow().ToUniversalTime());
        var saved = await _taskRepository
            .UpdateAsync(existing, command.ExpectedVersion, transaction, cancellationToken)
            .ConfigureAwait(false);
        var pendingOffsets = reminders
            .Where(reminder => reminder.Status == ReminderStatus.Pending)
            .Select(reminder => reminder.RelativeOffsetMinutes)
            .ToHashSet();
        var restoredCount = 0;
        if (saved.Entity.Deadline is not null)
        {
            foreach (var offset in reminders
                .Where(reminder => reminder.Status == ReminderStatus.Cancelled)
                .Select(reminder => reminder.RelativeOffsetMinutes)
                .Distinct()
                .Where(offset => !pendingOffsets.Contains(offset)))
            {
                var restored = CreateReminder(saved.Entity, offset);
                await _reminderRepository.AddAsync(restored, transaction, cancellationToken).ConfigureAwait(false);
                restoredCount++;
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        var affectedDates = AffectedDates(existing, saved.Entity);
        var events = new List<IApplicationEvent>
        {
            new TaskCompletedChanged(saved.Entity.Id, saved.NewVersion, affectedDates, IsCompleted: false)
        };
        if (restoredCount > 0)
        {
            events.Add(new ReminderPlanChanged(
                saved.Entity.Id,
                saved.NewVersion,
                affectedDates,
                HasPendingPlan: true));
        }

        var eventStatus = await PublishAfterCommitAsync(events, cancellationToken).ConfigureAwait(false);
        return ApplicationResult<TaskDto>.Success(ToDto(saved.Entity), WarningsFor(saved.Entity), eventStatus);
    }

    private async Task<ApplicationResult<TaskDto>> StartProcessingCoreAsync(
        ChangeTaskStateCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false);
        var existing = await _taskRepository.GetByIdAsync(command.TaskId, transaction, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return ApplicationResult<TaskDto>.Failure(NotFound("Task.NotFound", "The requested task could not be found."));
        }

        var expectedVersionError = ValidateExpectedVersion(existing, command.ExpectedVersion);
        if (expectedVersionError is not null)
        {
            return ApplicationResult<TaskDto>.Failure(expectedVersionError);
        }

        if (existing.WorkflowStatus == WorkflowStatus.InProgress)
        {
            return ApplicationResult<TaskDto>.Success(ToDto(existing));
        }

        existing.StartProcessing(_timeProvider.GetUtcNow().ToUniversalTime());
        var saved = await _taskRepository
            .UpdateAsync(existing, command.ExpectedVersion, transaction, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        var eventStatus = await PublishAfterCommitAsync(
            new[] { new TaskUpdated(saved.Entity.Id, saved.NewVersion, AffectedDates(existing, saved.Entity), false) },
            cancellationToken).ConfigureAwait(false);
        return ApplicationResult<TaskDto>.Success(ToDto(saved.Entity), WarningsFor(saved.Entity), eventStatus);
    }

    private async Task<ApplicationResult<DeletedTaskDto>> DeleteCoreAsync(
        DeleteTaskCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false);
        var existing = await _taskRepository.GetByIdAsync(command.TaskId, transaction, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return ApplicationResult<DeletedTaskDto>.Failure(NotFound("Task.NotFound", "The requested task could not be found."));
        }

        var expectedVersionError = ValidateExpectedVersion(existing, command.ExpectedVersion);
        if (expectedVersionError is not null)
        {
            return ApplicationResult<DeletedTaskDto>.Failure(expectedVersionError);
        }

        if (existing.SeriesId.HasValue)
        {
            return ApplicationResult<DeletedTaskDto>.Failure(ApplicationErrorMapper.Validation(
                "Task.RecurrenceDeleteDeferred",
                "Deleting a recurrence instance is handled by the recurrence use case."));
        }

        var affectedDates = AffectedDates(existing, existing);
        await _taskRepository
            .DeleteAsync(existing.Id, command.ExpectedVersion, transaction, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        var deleted = new DeletedTaskDto(existing.Id, existing.Version, affectedDates);
        var events = new IApplicationEvent[]
        {
            new TaskDeleted(existing.Id, existing.Version, affectedDates),
            new ReminderPlanChanged(existing.Id, existing.Version, affectedDates, HasPendingPlan: false)
        };
        var eventStatus = await PublishAfterCommitAsync(events, cancellationToken).ConfigureAwait(false);
        return ApplicationResult<DeletedTaskDto>.Success(
            deleted,
            postCommitEventStatus: eventStatus);
    }

}
