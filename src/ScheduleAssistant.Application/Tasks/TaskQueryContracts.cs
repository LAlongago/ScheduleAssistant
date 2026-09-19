using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Tasks;

/// <summary>Query for tasks shown on one local calendar date.</summary>
public sealed record GetCalendarDateQuery(DateOnly Date);

/// <summary>Query for uncompleted tasks aggregated into today's pending area.</summary>
public sealed record GetTodayPendingQuery;

/// <summary>Query for the week containing the supplied local date.</summary>
public sealed record GetWeekCalendarQuery(DateOnly Date);

/// <summary>Query for the month containing the supplied local date.</summary>
public sealed record GetMonthCalendarQuery(DateOnly Date);

/// <summary>Supported future deadline windows and the all-deadlines view.</summary>
public enum DeadlineQueryRange
{
    Next24Hours = 0,
    Next3Days = 1,
    Next7Days = 2,
    Next30Days = 3,
    All = 4
}

/// <summary>Query for incomplete tasks with deadlines in a supported window.</summary>
public sealed record GetDeadlinesQuery(DeadlineQueryRange Range = DeadlineQueryRange.All);

/// <summary>Query for all tasks or a filtered, stable, one-based page of tasks.</summary>
public sealed record SearchTasksQuery(
    string? Keyword = null,
    Guid? CategoryId = null,
    TaskPriority? Priority = null,
    WorkflowStatus? WorkflowStatus = null,
    bool? IsOverdue = null,
    int PageNumber = 1,
    int PageSize = 50);
