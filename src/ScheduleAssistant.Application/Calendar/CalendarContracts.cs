using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Calendar;

/// <summary>
/// A task occurrence shown on one calendar date. The same task may have a separate
/// entry on another date, while planned and deadline markers on one date are merged.
/// </summary>
public sealed record CalendarEntry(
    TaskDto Task,
    DateOnly DisplayDate,
    bool IsPlannedOnDate,
    bool IsDeadlineOnDate,
    DisplayStatus DisplayStatus,
    DeadlineUrgencyLevel DeadlineUrgency);

/// <summary>All entries for one date in a date, week, or month query.</summary>
public sealed record CalendarDayDto(
    DateOnly Date,
    bool IsInDisplayedMonth,
    IReadOnlyList<CalendarEntry> Entries);

/// <summary>The fixed Monday-to-Sunday result for a week query.</summary>
public sealed record WeekCalendarDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    IReadOnlyList<CalendarDayDto> Days);

/// <summary>The fixed six-row, 42-day result for a month query.</summary>
public sealed record MonthCalendarDto(
    DateOnly Month,
    DateOnly GridStart,
    DateOnly GridEnd,
    IReadOnlyList<CalendarDayDto> Days);

/// <summary>Uncompleted tasks aggregated into today's pending area.</summary>
public sealed record TodayPendingDto(
    DateOnly Date,
    IReadOnlyList<CalendarEntry> Entries);

/// <summary>Separates future deadlines from deadlines already past due.</summary>
public sealed record DeadlineQueryResult(
    IReadOnlyList<CalendarEntry> Upcoming,
    IReadOnlyList<CalendarEntry> Overdue);
