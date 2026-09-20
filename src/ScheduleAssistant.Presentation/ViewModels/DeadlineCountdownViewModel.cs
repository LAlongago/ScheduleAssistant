using CommunityToolkit.Mvvm.ComponentModel;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Cached display projection for one incomplete Deadline.</summary>
public sealed class DeadlineCountdownViewModel : ObservableObject
{
    private string _remainingText = string.Empty;

    /// <summary>Creates a countdown from an Application calendar entry.</summary>
    public DeadlineCountdownViewModel(CalendarEntry entry, CategoryOptionDto? category, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var deadline = entry.Task.Deadline
            ?? throw new ArgumentException("A countdown requires a task deadline.", nameof(entry));

        TaskId = entry.Task.Id;
        Version = entry.Task.Version;
        Title = entry.Task.Title;
        CategoryText = category?.Name ?? "未分类";
        CategoryColorHex = category?.ColorHex;
        PriorityText = FormatPriority(entry.Task.Priority);
        DeadlineText = DeadlineTextFormatter.FormatDeadline(deadline);
        Urgency = entry.DeadlineUrgency;
        UrgencyText = FormatUrgency(entry.DeadlineUrgency);
        DeadlineUtc = deadline.Utc;
        Update(nowUtc);
    }

    /// <summary>Gets the stable task identity.</summary>
    public Guid TaskId { get; }

    /// <summary>Gets the task version represented by this cached projection.</summary>
    public long Version { get; }

    /// <summary>Gets the task title.</summary>
    public string Title { get; }

    /// <summary>Gets the category label.</summary>
    public string CategoryText { get; }

    /// <summary>Gets the persisted category color.</summary>
    public string? CategoryColorHex { get; }

    /// <summary>Gets the priority label.</summary>
    public string PriorityText { get; }

    /// <summary>Gets the accurately formatted local Deadline.</summary>
    public string DeadlineText { get; }

    /// <summary>Gets the Deadline urgency returned by Application.</summary>
    public DeadlineUrgencyLevel Urgency { get; }

    /// <summary>Gets the text label for the cached urgency.</summary>
    public string UrgencyText { get; }

    /// <summary>Gets the UTC instant used by the one-shot refresh timer.</summary>
    public DateTimeOffset DeadlineUtc { get; }

    /// <summary>Gets the cached remaining duration text.</summary>
    public string RemainingText
    {
        get => _remainingText;
        private set => SetProperty(ref _remainingText, value);
    }

    /// <summary>Updates only countdown text from the injected clock; it never queries storage.</summary>
    public void Update(DateTimeOffset nowUtc)
    {
        RemainingText = DeadlineTextFormatter.FormatRemaining(DeadlineUtc, nowUtc);
    }

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
            _ => string.Empty
        };
    }
}
