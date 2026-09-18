using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;

namespace ScheduleAssistant.Application.Tasks;

public sealed partial class TaskUseCases
{
    /// <inheritdoc />
    public Task<ApplicationResult<CalendarDayDto>> GetCalendarDateAsync(
        GetCalendarDateQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => GetCalendarDateCoreAsync(query, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<TodayPendingDto>> GetTodayPendingAsync(
        GetTodayPendingQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => GetTodayPendingCoreAsync(query, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<WeekCalendarDto>> GetWeekCalendarAsync(
        GetWeekCalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => GetWeekCalendarCoreAsync(query, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<MonthCalendarDto>> GetMonthCalendarAsync(
        GetMonthCalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => GetMonthCalendarCoreAsync(query, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<DeadlineQueryResult>> GetDeadlinesAsync(
        GetDeadlinesQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => GetDeadlinesCoreAsync(query, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<PagedResult<TaskDto>>> SearchAsync(
        SearchTasksQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() => SearchCoreAsync(query, cancellationToken));
    }
}
