using ScheduleAssistant.Application.Tasks;

namespace ScheduleAssistant.Application.Recurrence;

/// <summary>Application transport code for the V1 recurrence frequencies.</summary>
public enum RecurrenceFrequencyCode
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Yearly = 3
}

/// <summary>Application transport mask for the weekdays selected by a weekly rule.</summary>
[Flags]
public enum RecurrenceWeekdayCode
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    All = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday
}

/// <summary>Editable recurrence rule input. V1 accepts interval one only.</summary>
public sealed record RecurrenceRuleDraft(
    RecurrenceFrequencyCode Frequency,
    DateOnly EffectiveDate,
    string TimeZoneId,
    DateOnly? EndDate = null,
    RecurrenceWeekdayCode Weekdays = RecurrenceWeekdayCode.None,
    int? MonthDay = null,
    int? YearMonth = null,
    int? YearDay = null,
    int Interval = 1);

/// <summary>Shared fields used to create or update a recurrence series.</summary>
public sealed record RecurrenceSeriesDraft(
    string Title,
    Guid CategoryId,
    TaskPriorityCode Priority,
    RecurrenceRuleDraft Rule,
    TimeOnly? PlannedStart = null,
    TimeOnly? PlannedEnd = null,
    string? Location = null,
    string? Description = null,
    string? Materials = null,
    string? Notes = null,
    bool IsEnabled = true);

/// <summary>Immutable recurrence rule data returned to an Application caller.</summary>
public sealed record RecurrenceRuleDto(
    RecurrenceFrequencyCode Frequency,
    DateOnly EffectiveDate,
    string TimeZoneId,
    DateOnly? EndDate,
    RecurrenceWeekdayCode Weekdays,
    int? MonthDay,
    int? YearMonth,
    int? YearDay,
    int Interval);

/// <summary>Immutable recurrence series data returned to an Application caller.</summary>
public sealed record RecurrenceSeriesDto(
    Guid Id,
    string Title,
    Guid CategoryId,
    TaskPriorityCode Priority,
    RecurrenceRuleDto Rule,
    TimeOnly? PlannedStart,
    TimeOnly? PlannedEnd,
    string? Location,
    string? Description,
    string? Materials,
    string? Notes,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    long Version);

/// <summary>Command for creating a recurrence series.</summary>
public sealed record CreateRecurrenceSeriesCommand(RecurrenceSeriesDraft Draft);

/// <summary>Query for one recurrence series.</summary>
public sealed record GetRecurrenceSeriesQuery(Guid SeriesId);

/// <summary>Command for changing shared fields from a selected occurrence date onward.</summary>
public sealed record UpdateRecurrenceSeriesCommand(
    Guid SeriesId,
    long ExpectedVersion,
    RecurrenceSeriesDraft Draft,
    DateOnly? ApplyFromDate = null);

/// <summary>Command for disabling future materialization without deleting history.</summary>
public sealed record DeactivateRecurrenceSeriesCommand(Guid SeriesId, long ExpectedVersion);

/// <summary>Command for deleting one occurrence and recording its exclusion atomically.</summary>
public sealed record DeleteRecurrenceInstanceCommand(Guid TaskId, long ExpectedVersion);

/// <summary>Command for deleting future unfinished ordinary instances and truncating the series.</summary>
public sealed record DeleteFutureRecurrenceCommand(
    Guid SeriesId,
    long ExpectedVersion,
    DateOnly FromDate);

/// <summary>Value returned after deleting one recurrence occurrence.</summary>
public sealed record DeletedRecurrenceInstanceDto(
    Guid TaskId,
    Guid SeriesId,
    DateOnly OccurrenceDate,
    IReadOnlyList<DateOnly> AffectedDates);

/// <summary>Value returned after deleting a future recurrence range.</summary>
public sealed record DeletedFutureRecurrenceDto(
    Guid SeriesId,
    DateOnly FromDate,
    IReadOnlyList<Guid> DeletedTaskIds,
    long NewVersion,
    IReadOnlyList<DateOnly> AffectedDates);

/// <summary>One task instance inserted by a materialization transaction.</summary>
public sealed record MaterializedRecurrenceTask(
    Guid TaskId,
    Guid SeriesId,
    DateOnly OccurrenceDate,
    long Version);
