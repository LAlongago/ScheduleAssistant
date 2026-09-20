using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Presentation.Composition;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Creates real task-card projections from Application calendar entries.</summary>
public interface ITaskCardMapper
{
    /// <summary>Maps one Application entry and its category metadata to a task card.</summary>
    TaskCardViewModel Map(
        CalendarEntry entry,
        IReadOnlyDictionary<Guid, CategoryOptionDto> categories,
        TaskCardMode mode = TaskCardMode.Standard);
}

/// <summary>Presentation mapper that keeps Application and WPF contracts separate.</summary>
public sealed class TaskCardMapper : ITaskCardMapper
{
    private readonly ITaskUseCases _taskUseCases;
    private readonly IUiDispatcher _dispatcher;

    /// <summary>Initializes the mapper with the existing task use cases.</summary>
    public TaskCardMapper(ITaskUseCases taskUseCases, IUiDispatcher dispatcher)
    {
        _taskUseCases = taskUseCases ?? throw new ArgumentNullException(nameof(taskUseCases));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public TaskCardViewModel Map(
        CalendarEntry entry,
        IReadOnlyDictionary<Guid, CategoryOptionDto> categories,
        TaskCardMode mode = TaskCardMode.Standard)
    {
        ArgumentNullException.ThrowIfNull(categories);
        categories.TryGetValue(entry.Task.CategoryId, out var category);
        return new TaskCardViewModel(
            CreatePresentation(entry, category, mode),
            _taskUseCases,
            _dispatcher);
    }

    private static TaskCardPresentation CreatePresentation(
        CalendarEntry entry,
        CategoryOptionDto? category,
        TaskCardMode mode)
    {
        var task = entry.Task;
        return new TaskCardPresentation(
            task,
            category?.Name ?? "未分类",
            category?.ColorHex,
            FormatPlannedTime(task, entry.IsPlannedOnDate),
            FormatDeadline(task, entry.IsDeadlineOnDate),
            entry.IsPlannedOnDate ? "● 计划" : string.Empty,
            entry.IsDeadlineOnDate ? "◆ 今日 Deadline" : string.Empty,
            FormatPriority(task.Priority),
            task.Location?.Trim() ?? string.Empty,
            FormatDisplayStatus(entry.DisplayStatus),
            FormatUrgency(entry.DeadlineUrgency),
            entry.IsPlannedOnDate,
            entry.IsDeadlineOnDate,
            entry.DisplayStatus,
            entry.DeadlineUrgency,
            mode);
    }

    private static string FormatPlannedTime(TaskDto task, bool plannedOnDate)
    {
        if (!task.PlannedDate.HasValue)
        {
            return "无计划";
        }

        var dateText = FormatDate(task.PlannedDate.Value);
        var timeText = task.PlannedStart.HasValue
            ? $" · {task.PlannedStart.Value:HH\\:mm}{(task.PlannedEnd.HasValue ? $"–{task.PlannedEnd.Value:HH\\:mm}" : string.Empty)}"
            : " · 全天";
        var marker = plannedOnDate ? "计划" : "原计划";
        return $"● {marker} · {dateText}{timeText}";
    }

    private static string FormatDeadline(TaskDto task, bool deadlineOnDate)
    {
        if (task.Deadline is null)
        {
            return "无 Deadline";
        }

        var marker = deadlineOnDate ? "◆ 今日 Deadline" : "◆ Deadline";
        return $"{marker} · {FormatDate(task.Deadline.LocalDate)} {task.Deadline.LocalTime:HH\\:mm}";
    }

    private static string FormatDate(DateOnly date) => $"{date.Year}年{date.Month}月{date.Day}日";

    private static string FormatPriority(TaskPriority priority)
    {
        return priority switch
        {
            TaskPriority.Low => "低优先级",
            TaskPriority.Normal => "一般",
            TaskPriority.Important => "重要",
            TaskPriority.UrgentAndImportant => "紧急且重要",
            _ => "未指定优先级"
        };
    }

    private static string FormatDisplayStatus(DisplayStatus status)
    {
        return status switch
        {
            DisplayStatus.Completed => "已完成",
            DisplayStatus.Overdue => "已逾期",
            DisplayStatus.InProgress => "进行中",
            DisplayStatus.PlannedPast => "计划已过",
            DisplayStatus.NotStarted => "未开始",
            _ => "状态未知"
        };
    }

    private static string FormatUrgency(DeadlineUrgencyLevel urgency)
    {
        return urgency switch
        {
            DeadlineUrgencyLevel.None => string.Empty,
            DeadlineUrgencyLevel.Neutral => "从容",
            DeadlineUrgencyLevel.MoreThanThreeDays => "3 天以上",
            DeadlineUrgencyLevel.OneToThreeDays => "1–3 天",
            DeadlineUrgencyLevel.LessThanOneDay => "24 小时内",
            DeadlineUrgencyLevel.Overdue => "已逾期",
            _ => ""
        };
    }
}

/// <summary>Immutable display values produced from one Application calendar entry.</summary>
public sealed record TaskCardPresentation(
    TaskDto Task,
    string CategoryText,
    string? CategoryColorHex,
    string PlannedTimeText,
    string DeadlineText,
    string PlanMarkerText,
    string DeadlineMarkerText,
    string PriorityText,
    string LocationText,
    string StatusText,
    string UrgencyText,
    bool IsPlannedOnDate,
    bool IsDeadlineOnDate,
    DisplayStatus DisplayStatus,
    DeadlineUrgencyLevel DeadlineUrgency,
    TaskCardMode Mode);

/// <summary>Formatting helpers for cached Deadline countdown text.</summary>
public static class DeadlineTextFormatter
{
    /// <summary>Formats a stored local Deadline for display.</summary>
    public static string FormatDeadline(DeadlineDto deadline)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        return $"{deadline.LocalDate.Year}年{deadline.LocalDate.Month}月{deadline.LocalDate.Day}日 {deadline.LocalTime:HH\\:mm}";
    }

    /// <summary>Formats the current remaining duration without querying persistence.</summary>
    public static string FormatRemaining(DateTimeOffset deadlineUtc, DateTimeOffset nowUtc)
    {
        var remaining = deadlineUtc.ToUniversalTime() - nowUtc.ToUniversalTime();
        var isOverdue = remaining < TimeSpan.Zero;
        var duration = remaining.Duration();
        var parts = new List<string>(capacity: 3);
        if (duration.Days > 0)
        {
            parts.Add($"{duration.Days} 天");
        }

        if (duration.Hours > 0)
        {
            parts.Add($"{duration.Hours} 小时");
        }

        if (duration.Minutes > 0 || parts.Count == 0)
        {
            parts.Add(duration.Minutes > 0 ? $"{duration.Minutes} 分钟" : "不到 1 分钟");
        }

        return isOverdue ? $"已逾期 {string.Join(" ", parts)}" : $"剩余 {string.Join(" ", parts)}";
    }
}
