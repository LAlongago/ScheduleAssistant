using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Recurrence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Tasks;

public sealed partial class TaskUseCases
{
    private const int MaximumSearchPageSize = 200;

    private async Task<ApplicationResult<CalendarDayDto>> GetCalendarDateCoreAsync(
        GetCalendarDateQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (_recurrenceMaterializer is not null)
        {
            await _recurrenceMaterializer
                .MaterializeAsync(query.Date, query.Date, cancellationToken)
                .ConfigureAwait(false);
        }
        var snapshot = CaptureQueryTime();
        var tasks = await _taskRepository
            .GetByRangeAsync(query.Date, query.Date, cancellationToken)
            .ConfigureAwait(false);
        var entries = BuildEntriesForDate(tasks, query.Date, snapshot, CalendarSortMode.Day);
        return ApplicationResult<CalendarDayDto>.Success(
            new CalendarDayDto(query.Date, IsInDisplayedMonth: true, entries));
    }

    private async Task<ApplicationResult<TodayPendingDto>> GetTodayPendingCoreAsync(
        GetTodayPendingQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = CaptureQueryTime();
        if (_recurrenceMaterializer is not null)
        {
            await _recurrenceMaterializer
                .MaterializeAsync(snapshot.TodayLocal, snapshot.TodayLocal, cancellationToken)
                .ConfigureAwait(false);
        }
        var tasks = await _taskRepository
            .GetTodayPendingAsync(snapshot.TodayLocal, snapshot.NowUtc, cancellationToken)
            .ConfigureAwait(false);
        var entries = BuildTodayEntries(tasks, snapshot);
        return ApplicationResult<TodayPendingDto>.Success(
            new TodayPendingDto(snapshot.TodayLocal, entries));
    }

    private async Task<ApplicationResult<WeekCalendarDto>> GetWeekCalendarCoreAsync(
        GetWeekCalendarQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = CaptureQueryTime();
        var weekStart = StartOfWeek(query.Date);
        var weekEnd = weekStart.AddDays(6);
        if (_recurrenceMaterializer is not null)
        {
            await _recurrenceMaterializer
                .MaterializeAsync(weekStart, weekEnd, cancellationToken)
                .ConfigureAwait(false);
        }
        var tasks = await _taskRepository
            .GetByRangeAsync(weekStart, weekEnd, cancellationToken)
            .ConfigureAwait(false);
        var days = BuildCalendarDays(
            tasks,
            weekStart,
            count: 7,
            snapshot,
            CalendarSortMode.Day,
            static _ => true);
        return ApplicationResult<WeekCalendarDto>.Success(
            new WeekCalendarDto(weekStart, weekEnd, days));
    }

    private async Task<ApplicationResult<MonthCalendarDto>> GetMonthCalendarCoreAsync(
        GetMonthCalendarQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = CaptureQueryTime();
        var month = new DateOnly(query.Date.Year, query.Date.Month, 1);
        var gridStart = StartOfWeek(month);
        var gridEnd = gridStart.AddDays(41);
        if (_recurrenceMaterializer is not null)
        {
            await _recurrenceMaterializer
                .MaterializeAsync(gridStart, gridEnd, cancellationToken)
                .ConfigureAwait(false);
        }
        var tasks = await _taskRepository
            .GetByRangeAsync(gridStart, gridEnd, cancellationToken)
            .ConfigureAwait(false);
        var days = BuildCalendarDays(
            tasks,
            gridStart,
            count: 42,
            snapshot,
            CalendarSortMode.Month,
            date => date.Year == month.Year && date.Month == month.Month);
        return ApplicationResult<MonthCalendarDto>.Success(
            new MonthCalendarDto(month, gridStart, gridEnd, days));
    }

    private async Task<ApplicationResult<DeadlineQueryResult>> GetDeadlinesCoreAsync(
        GetDeadlinesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var rangeError = ValidateDeadlineRange(query.Range);
        if (rangeError is not null)
        {
            return ApplicationResult<DeadlineQueryResult>.Failure(rangeError);
        }

        var snapshot = CaptureQueryTime();
        var includeOverdue = query.Range == DeadlineQueryRange.All;
        DateTimeOffset? untilUtc = query.Range switch
        {
            DeadlineQueryRange.Next24Hours => snapshot.NowUtc.AddHours(24),
            DeadlineQueryRange.Next3Days => snapshot.NowUtc.AddDays(3),
            DeadlineQueryRange.Next7Days => snapshot.NowUtc.AddDays(7),
            DeadlineQueryRange.Next30Days => snapshot.NowUtc.AddDays(30),
            DeadlineQueryRange.All => null,
            _ => throw new InvalidOperationException("An unsupported deadline range was supplied.")
        };
        var tasks = await _taskRepository
            .GetDeadlinesAsync(snapshot.NowUtc, untilUtc, includeOverdue, cancellationToken)
            .ConfigureAwait(false);
        var entries = tasks
            .Where(task => task.Deadline is not null)
            .Select(task => ToCalendarEntry(task, task.Deadline!.LocalDate, snapshot))
            .ToArray();
        var overdue = entries
            .Where(entry => entry.Task.Deadline is not null && entry.Task.Deadline.Utc < snapshot.NowUtc)
            .OrderBy(entry => entry.Task.Deadline!.Utc)
            .ThenByDescending(entry => entry.Task.Priority)
            .ThenBy(entry => entry.Task.CreatedAtUtc)
            .ThenBy(entry => entry.Task.Id)
            .ToArray();
        var upcoming = entries
            .Where(entry => entry.Task.Deadline is not null && entry.Task.Deadline.Utc >= snapshot.NowUtc)
            .OrderBy(entry => entry.Task.Deadline!.Utc)
            .ThenByDescending(entry => entry.Task.Priority)
            .ThenBy(entry => entry.Task.CreatedAtUtc)
            .ThenBy(entry => entry.Task.Id)
            .ToArray();
        return ApplicationResult<DeadlineQueryResult>.Success(
            new DeadlineQueryResult(upcoming, overdue));
    }

    private async Task<ApplicationResult<PagedResult<TaskDto>>> SearchCoreAsync(
        SearchTasksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var validationError = ValidateSearchQuery(query, out var offset);
        if (validationError is not null)
        {
            return ApplicationResult<PagedResult<TaskDto>>.Failure(validationError);
        }

        var snapshot = CaptureQueryTime();
        var filter = new TaskSearchFilter(
            NormalizeKeyword(query.Keyword),
            query.CategoryId,
            query.Priority.HasValue ? TaskContractMapper.ToDomain(query.Priority.Value) : null,
            query.WorkflowStatus.HasValue ? TaskContractMapper.ToDomain(query.WorkflowStatus.Value) : null,
            query.IsOverdue,
            snapshot.NowUtc);
        var totalCount = await _taskRepository
            .CountSearchAsync(filter, cancellationToken)
            .ConfigureAwait(false);
        var tasks = await _taskRepository
            .SearchAsync(filter, offset, query.PageSize, cancellationToken)
            .ConfigureAwait(false);
        var page = new PagedResult<TaskDto>(
            tasks.Select(ToDto).ToArray(),
            totalCount,
            query.PageNumber,
            query.PageSize);
        return ApplicationResult<PagedResult<TaskDto>>.Success(page);
    }
}
