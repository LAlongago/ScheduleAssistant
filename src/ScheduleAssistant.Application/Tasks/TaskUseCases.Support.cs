using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Tasks;

public sealed partial class TaskUseCases
{
    private async Task<bool> CreateReminderIfApplicableAsync(
        TaskItem task,
        ReminderPlanInput plan,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (task.Deadline is null || task.WorkflowStatus == WorkflowStatus.Completed || !plan.Enabled)
        {
            return false;
        }

        await _reminderRepository
            .AddAsync(CreateReminder(task, plan.RelativeOffsetMinutes), transaction, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private async Task<ReminderChange> MaintainUpdatedRemindersAsync(
        TaskItem task,
        ZonedDeadline? previousDeadline,
        bool deadlineChanged,
        ReminderPlanInput? requestedPlan,
        IPersistenceTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (!deadlineChanged && requestedPlan is null)
        {
            return ReminderChange.Unchanged;
        }

        var reminders = await _reminderRepository
            .GetByTaskIdAsync(task.Id, transaction, cancellationToken)
            .ConfigureAwait(false);
        var previousPlanOffset = reminders
            .Where(reminder => reminder.Status != ReminderStatus.Cancelled)
            .Select(reminder => (int?)reminder.RelativeOffsetMinutes)
            .FirstOrDefault();
        var cancelledCount = await _reminderRepository
            .CancelPendingByTaskIdAsync(task.Id, transaction, cancellationToken)
            .ConfigureAwait(false);

        var shouldCreate = task.Deadline is not null && task.WorkflowStatus != WorkflowStatus.Completed;
        var deadlineWasAdded = previousDeadline is null && task.Deadline is not null;
        var enabled = requestedPlan?.Enabled
            ?? (previousPlanOffset.HasValue || deadlineWasAdded && reminders.Count == 0);
        var offset = requestedPlan?.RelativeOffsetMinutes ?? previousPlanOffset ?? DefaultReminderOffsetMinutes;
        var created = false;
        if (shouldCreate && enabled)
        {
            await _reminderRepository.AddAsync(CreateReminder(task, offset), transaction, cancellationToken).ConfigureAwait(false);
            created = true;
        }

        var changed = deadlineChanged || requestedPlan is not null || cancelledCount > 0 || created;
        return new ReminderChange(changed, created, created);
    }

    private Reminder CreateReminder(TaskItem task, int relativeOffsetMinutes)
    {
        var deadlineUtc = task.DeadlineUtc
            ?? throw new InvalidOperationException("A reminder requires a task deadline.");
        var id = _newId();
        var scheduledAtUtc = deadlineUtc.AddMinutes(relativeOffsetMinutes);
        var key = $"task:{task.Id:N}:deadline:{deadlineUtc:O}:offset:{relativeOffsetMinutes}:node:{id:N}";
        return Reminder.Create(id, task.Id, relativeOffsetMinutes, scheduledAtUtc, key);
    }

    private DeadlineResolution ResolveDeadline(DeadlineInput? input)
    {
        return input is null
            ? new DeadlineResolution(DeadlineResolutionStatus.Resolved, Deadline: null, Error: null)
            : _deadlineResolver.Resolve(input);
    }

    private static ApplicationError? ValidateCategory(Category? category)
    {
        if (category is null)
        {
            return NotFound("Category.NotFound", "The selected category could not be found.");
        }

        return category.IsArchived
            ? ApplicationErrorMapper.Validation("Category.Archived", "The selected category is archived.")
            : null;
    }

    private static ApplicationError? ValidateExpectedVersion(TaskItem task, long expectedVersion)
    {
        return task.Version == expectedVersion
            ? null
            : new ApplicationError(
                ApplicationErrorKind.Conflict,
                "Persistence.Conflict",
                "The item was changed elsewhere. Reload it and try again.");
    }

    private static ApplicationError NotFound(string code, string message)
    {
        return new ApplicationError(ApplicationErrorKind.NotFound, code, message);
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

    private static TaskDto ToDto(TaskItem task)
    {
        return new TaskDto(
            task.Id,
            task.Title,
            task.CategoryId,
            task.Priority,
            task.WorkflowStatus,
            task.PlannedDate,
            task.PlannedStart,
            task.PlannedEnd,
            task.Deadline is null
                ? null
                : new DeadlineDto(
                    task.Deadline.LocalDate,
                    task.Deadline.LocalTime,
                    task.Deadline.TimeZoneId,
                    task.Deadline.Utc),
            task.Location,
            task.Description,
            task.Materials,
            task.Notes,
            task.SeriesId,
            task.OccurrenceDate?.Date,
            task.IsOccurrenceOverride,
            task.CreatedAtUtc,
            task.UpdatedAtUtc,
            task.CompletedAtUtc,
            task.Version);
    }

    private static ApplicationWarning[] WarningsFor(TaskItem task)
    {
        return task.PlannedDate.HasValue
            && task.Deadline is not null
            && task.Deadline.LocalDate < task.PlannedDate.Value
            ? new[]
            {
                new ApplicationWarning(
                    "Deadline.BeforePlannedDate",
                    "The deadline is earlier than the planned date. Review the schedule.")
            }
            : Array.Empty<ApplicationWarning>();
    }

    private static DateOnly[] AffectedDates(TaskItem? previous, TaskItem? current)
    {
        var dates = new HashSet<DateOnly>();
        AddDates(dates, previous);
        AddDates(dates, current);
        return dates.OrderBy(date => date).ToArray();
    }

    private static DateOnly[] AffectedDatesFromValues(
        IReadOnlyList<DateOnly> previousDates,
        TaskItem current)
    {
        var dates = new HashSet<DateOnly>(previousDates);
        AddDates(dates, current);
        return dates.OrderBy(date => date).ToArray();
    }

    private static void AddDates(HashSet<DateOnly> dates, TaskItem? task)
    {
        if (task?.PlannedDate is DateOnly plannedDate)
        {
            dates.Add(plannedDate);
        }

        if (task?.Deadline is not null)
        {
            dates.Add(task.Deadline.LocalDate);
        }
    }

    private readonly record struct ReminderChange(bool Changed, bool Added, bool HasPendingPlan)
    {
        public static ReminderChange Unchanged => new(false, false, false);
    }
}
