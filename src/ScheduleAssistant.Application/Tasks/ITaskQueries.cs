using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;

namespace ScheduleAssistant.Application.Tasks;

/// <summary>Application read boundary for calendar, deadline, and task-search views.</summary>
public interface ITaskQueries
{
    /// <summary>Gets one local date without aggregating historical tasks into it.</summary>
    Task<ApplicationResult<CalendarDayDto>> GetCalendarDateAsync(
        GetCalendarDateQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets uncompleted tasks for today's pending area, including historical work.</summary>
    Task<ApplicationResult<TodayPendingDto>> GetTodayPendingAsync(
        GetTodayPendingQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a fixed Monday-to-Sunday calendar.</summary>
    Task<ApplicationResult<WeekCalendarDto>> GetWeekCalendarAsync(
        GetWeekCalendarQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a fixed 42-day month grid including adjacent-month dates.</summary>
    Task<ApplicationResult<MonthCalendarDto>> GetMonthCalendarAsync(
        GetMonthCalendarQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets incomplete deadline entries split into upcoming and overdue groups.</summary>
    Task<ApplicationResult<DeadlineQueryResult>> GetDeadlinesAsync(
        GetDeadlinesQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a database-filtered and database-paged task search result.</summary>
    Task<ApplicationResult<PagedResult<TaskDto>>> SearchAsync(
        SearchTasksQuery query,
        CancellationToken cancellationToken = default);
}
