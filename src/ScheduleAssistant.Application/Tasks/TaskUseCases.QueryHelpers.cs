using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Tasks;

public sealed partial class TaskUseCases
{
    private static CalendarDayDto[] BuildCalendarDays(
        IReadOnlyList<TaskItem> tasks,
        DateOnly start,
        int count,
        QueryTimeSnapshot snapshot,
        CalendarSortMode sortMode,
        Func<DateOnly, bool> isInDisplayedMonth)
    {
        var days = new CalendarDayDto[count];
        for (var index = 0; index < count; index++)
        {
            var date = start.AddDays(index);
            days[index] = new CalendarDayDto(
                date,
                isInDisplayedMonth(date),
                BuildEntriesForDate(tasks, date, snapshot, sortMode));
        }

        return days;
    }

    private static CalendarEntry[] BuildEntriesForDate(
        IReadOnlyList<TaskItem> tasks,
        DateOnly date,
        QueryTimeSnapshot snapshot,
        CalendarSortMode sortMode)
    {
        var entries = new Dictionary<(Guid TaskId, DateOnly DisplayDate), CalendarEntry>();
        foreach (var task in tasks)
        {
            var isPlannedOnDate = task.PlannedDate == date;
            var isDeadlineOnDate = task.Deadline?.LocalDate == date;
            if (!isPlannedOnDate && !isDeadlineOnDate)
            {
                continue;
            }

            var key = (task.Id, date);
            if (entries.TryGetValue(key, out var existing))
            {
                entries[key] = existing with
                {
                    IsPlannedOnDate = existing.IsPlannedOnDate || isPlannedOnDate,
                    IsDeadlineOnDate = existing.IsDeadlineOnDate || isDeadlineOnDate
                };
            }
            else
            {
                entries.Add(key, ToCalendarEntry(task, date, snapshot));
            }
        }

        return SortEntries(entries.Values, sortMode, snapshot).ToArray();
    }

    private static CalendarEntry[] BuildTodayEntries(
        IReadOnlyList<TaskItem> tasks,
        QueryTimeSnapshot snapshot)
    {
        var entries = new Dictionary<(Guid TaskId, DateOnly DisplayDate), CalendarEntry>();
        foreach (var task in tasks)
        {
            var key = (task.Id, snapshot.TodayLocal);
            if (!entries.ContainsKey(key))
            {
                entries.Add(key, ToCalendarEntry(task, snapshot.TodayLocal, snapshot));
            }
        }

        return SortEntries(entries.Values, CalendarSortMode.Today, snapshot).ToArray();
    }

    private static CalendarEntry ToCalendarEntry(
        TaskItem task,
        DateOnly displayDate,
        QueryTimeSnapshot snapshot)
    {
        return new CalendarEntry(
            ToDto(task),
            displayDate,
            task.PlannedDate == displayDate,
            task.Deadline?.LocalDate == displayDate,
            TaskContractMapper.ToCode(
                DisplayStatusCalculator.Calculate(task, snapshot.NowUtc, snapshot.TodayLocal)),
            TaskContractMapper.ToCode(DeadlineUrgencyCalculator.Calculate(task, snapshot.NowUtc)));
    }

    private static IEnumerable<CalendarEntry> SortEntries(
        IEnumerable<CalendarEntry> entries,
        CalendarSortMode sortMode,
        QueryTimeSnapshot snapshot)
    {
        return sortMode switch
        {
            CalendarSortMode.Month => entries
                .OrderBy(entry => MonthPriority(entry, snapshot))
                .ThenByDescending(entry => entry.Task.Priority)
                .ThenBy(entry => entry.Task.PlannedStart.HasValue ? 1 : 0)
                .ThenBy(entry => entry.Task.PlannedStart)
                .ThenBy(entry => entry.Task.Deadline?.Utc)
                .ThenBy(entry => entry.Task.CreatedAtUtc)
                .ThenBy(entry => entry.Task.Id),
            CalendarSortMode.Today => entries
                .OrderBy(entry => TodayPriority(entry))
                .ThenBy(entry => entry.Task.PlannedStart.HasValue ? 1 : 0)
                .ThenBy(entry => entry.Task.PlannedStart)
                .ThenByDescending(entry => entry.Task.Priority)
                .ThenBy(entry => entry.Task.Deadline?.Utc)
                .ThenBy(entry => entry.Task.CreatedAtUtc)
                .ThenBy(entry => entry.Task.Id),
            _ => entries
                .OrderBy(entry => entry.Task.PlannedStart.HasValue ? 1 : 0)
                .ThenBy(entry => entry.Task.PlannedStart)
                .ThenByDescending(entry => entry.Task.Priority)
                .ThenBy(entry => entry.Task.Deadline?.Utc)
                .ThenBy(entry => entry.Task.CreatedAtUtc)
                .ThenBy(entry => entry.Task.Id)
        };
    }

    private static int MonthPriority(CalendarEntry entry, QueryTimeSnapshot snapshot)
    {
        if (entry.DisplayStatus == DisplayStatusCode.Overdue
            || entry.IsDeadlineOnDate && entry.DisplayDate == snapshot.TodayLocal)
        {
            return 0;
        }

        if (entry.Task.Priority >= TaskPriorityCode.Important)
        {
            return 1;
        }

        return entry.IsPlannedOnDate && entry.Task.PlannedStart.HasValue ? 2 : 3;
    }

    private static int TodayPriority(CalendarEntry entry)
    {
        if (entry.DisplayStatus == DisplayStatusCode.Overdue)
        {
            return 0;
        }

        if (entry.DisplayStatus == DisplayStatusCode.PlannedPast)
        {
            return 1;
        }

        if (entry.IsPlannedOnDate || entry.IsDeadlineOnDate)
        {
            return 2;
        }

        return 3;
    }

    private enum CalendarSortMode
    {
        Day,
        Month,
        Today
    }
}
