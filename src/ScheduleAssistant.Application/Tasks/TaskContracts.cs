using ScheduleAssistant.Application.Common;

namespace ScheduleAssistant.Application.Tasks;

/// <summary>Local deadline input supplied by a UI or another Application caller.</summary>
/// <param name="LocalDate">The user-entered local calendar date.</param>
/// <param name="LocalTime">The user-entered local wall-clock time.</param>
/// <param name="TimeZoneId">The explicit Windows time-zone identifier.</param>
/// <param name="ConfirmedUtc">
/// An explicitly confirmed UTC instant for an ambiguous local time. It must match one of the
/// time zone's valid offsets; it is never guessed by the Application.
/// </param>
public sealed record DeadlineInput(
    DateOnly LocalDate,
    TimeOnly LocalTime,
    string TimeZoneId,
    DateTimeOffset? ConfirmedUtc = null);

/// <summary>Controls the one V1 reminder plan created for a task.</summary>
/// <param name="Enabled">Whether a pending reminder should be maintained.</param>
/// <param name="RelativeOffsetMinutes">Minutes relative to the UTC deadline.</param>
public sealed record ReminderPlanInput(
    bool Enabled = true,
    int RelativeOffsetMinutes = -1_440);

/// <summary>Editable fields for an ordinary task or a materialized recurrence instance.</summary>
public sealed record TaskDraft(
    string Title,
    Guid CategoryId,
    TaskPriorityCode Priority = TaskPriorityCode.Normal,
    DateOnly? PlannedDate = null,
    TimeOnly? PlannedStart = null,
    TimeOnly? PlannedEnd = null,
    DeadlineInput? Deadline = null,
    string? Location = null,
    string? Description = null,
    string? Materials = null,
    string? Notes = null,
    ReminderPlanInput? ReminderPlan = null);

/// <summary>Command for creating an ordinary task.</summary>
public sealed record CreateTaskCommand(TaskDraft Draft);

/// <summary>Command for updating an ordinary task with an optimistic version.</summary>
public sealed record UpdateTaskCommand(Guid TaskId, long ExpectedVersion, TaskDraft Draft);

/// <summary>Command for a workflow state transition.</summary>
public sealed record ChangeTaskStateCommand(Guid TaskId, long ExpectedVersion);

/// <summary>Command for deleting an ordinary task with an optimistic version.</summary>
public sealed record DeleteTaskCommand(Guid TaskId, long ExpectedVersion);

/// <summary>Query for one task.</summary>
public sealed record GetTaskQuery(Guid TaskId);

/// <summary>Query for tasks planned on one local date.</summary>
public sealed record GetTasksByPlannedDateQuery(DateOnly PlannedDate);

/// <summary>Query for incomplete tasks with deadlines in a UTC interval.</summary>
public sealed record GetUpcomingDeadlinesQuery(DateTimeOffset? UntilUtc = null);

/// <summary>Query for active category choices.</summary>
public sealed record GetCategoryOptionsQuery;

/// <summary>Deadline value returned to Presentation without exposing a mutable domain object.</summary>
public sealed record DeadlineDto(
    DateOnly LocalDate,
    TimeOnly LocalTime,
    string TimeZoneId,
    DateTimeOffset Utc);

/// <summary>Task value returned by Application queries and commands.</summary>
public sealed record TaskDto(
    Guid Id,
    string Title,
    Guid CategoryId,
    TaskPriorityCode Priority,
    WorkflowStatusCode WorkflowStatus,
    DateOnly? PlannedDate,
    TimeOnly? PlannedStart,
    TimeOnly? PlannedEnd,
    DeadlineDto? Deadline,
    string? Location,
    string? Description,
    string? Materials,
    string? Notes,
    Guid? SeriesId,
    DateOnly? OccurrenceDate,
    bool IsOccurrenceOverride,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long Version);

/// <summary>Minimal category data needed by task editors and filters.</summary>
public sealed record CategoryOptionDto(
    Guid Id,
    string Name,
    string ColorHex,
    int SortOrder);

/// <summary>Value returned after a successful deletion.</summary>
public sealed record DeletedTaskDto(
    Guid TaskId,
    long Version,
    IReadOnlyList<DateOnly> AffectedDates);

/// <summary>Resolves a local deadline without relying on implicit machine time-zone behavior.</summary>
public enum DeadlineResolutionStatus
{
    /// <summary>The local value maps to exactly one UTC instant.</summary>
    Resolved,

    /// <summary>The local value is invalid and must be changed.</summary>
    RequiresModification,

    /// <summary>The local value has more than one valid UTC interpretation.</summary>
    RequiresConfirmation
}

/// <summary>Outcome of explicit deadline conversion.</summary>
public sealed record DeadlineResolution(
    DeadlineResolutionStatus Status,
    DeadlineDto? Deadline,
    ApplicationError? Error);
