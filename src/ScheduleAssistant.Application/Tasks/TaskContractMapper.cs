using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Tasks;

/// <summary>
/// The single translation boundary between Application transport codes and Domain enums.
/// Presentation consumes only the transport codes and never calls this mapper directly.
/// </summary>
public static class TaskContractMapper
{
    /// <summary>Maps a Domain task priority to its Application transport code.</summary>
    public static TaskPriorityCode ToCode(TaskPriority value) => value switch
    {
        TaskPriority.Low => TaskPriorityCode.Low,
        TaskPriority.Normal => TaskPriorityCode.Normal,
        TaskPriority.Important => TaskPriorityCode.Important,
        TaskPriority.UrgentAndImportant => TaskPriorityCode.UrgentAndImportant,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown task priority.")
    };

    /// <summary>Maps an Application task priority code to its Domain enum.</summary>
    public static TaskPriority ToDomain(TaskPriorityCode value) => value switch
    {
        TaskPriorityCode.Low => TaskPriority.Low,
        TaskPriorityCode.Normal => TaskPriority.Normal,
        TaskPriorityCode.Important => TaskPriority.Important,
        TaskPriorityCode.UrgentAndImportant => TaskPriority.UrgentAndImportant,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown task priority code.")
    };

    /// <summary>Maps a Domain workflow status to its Application transport code.</summary>
    public static WorkflowStatusCode ToCode(WorkflowStatus value) => value switch
    {
        WorkflowStatus.Pending => WorkflowStatusCode.Pending,
        WorkflowStatus.InProgress => WorkflowStatusCode.InProgress,
        WorkflowStatus.Completed => WorkflowStatusCode.Completed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown workflow status.")
    };

    /// <summary>Maps an Application workflow status code to its Domain enum.</summary>
    public static WorkflowStatus ToDomain(WorkflowStatusCode value) => value switch
    {
        WorkflowStatusCode.Pending => WorkflowStatus.Pending,
        WorkflowStatusCode.InProgress => WorkflowStatus.InProgress,
        WorkflowStatusCode.Completed => WorkflowStatus.Completed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown workflow status code.")
    };

    /// <summary>Maps a calculated Domain display status to its Application transport code.</summary>
    public static DisplayStatusCode ToCode(DisplayStatus value) => value switch
    {
        DisplayStatus.Completed => DisplayStatusCode.Completed,
        DisplayStatus.Overdue => DisplayStatusCode.Overdue,
        DisplayStatus.InProgress => DisplayStatusCode.InProgress,
        DisplayStatus.PlannedPast => DisplayStatusCode.PlannedPast,
        DisplayStatus.NotStarted => DisplayStatusCode.NotStarted,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown display status.")
    };

    /// <summary>Maps an Application display status code to its Domain enum.</summary>
    public static DisplayStatus ToDomain(DisplayStatusCode value) => value switch
    {
        DisplayStatusCode.Completed => DisplayStatus.Completed,
        DisplayStatusCode.Overdue => DisplayStatus.Overdue,
        DisplayStatusCode.InProgress => DisplayStatus.InProgress,
        DisplayStatusCode.PlannedPast => DisplayStatus.PlannedPast,
        DisplayStatusCode.NotStarted => DisplayStatus.NotStarted,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown display status code.")
    };

    /// <summary>Maps a calculated Domain urgency to its Application transport code.</summary>
    public static DeadlineUrgencyCode ToCode(DeadlineUrgencyLevel value) => value switch
    {
        DeadlineUrgencyLevel.None => DeadlineUrgencyCode.None,
        DeadlineUrgencyLevel.Neutral => DeadlineUrgencyCode.Neutral,
        DeadlineUrgencyLevel.MoreThanThreeDays => DeadlineUrgencyCode.MoreThanThreeDays,
        DeadlineUrgencyLevel.OneToThreeDays => DeadlineUrgencyCode.OneToThreeDays,
        DeadlineUrgencyLevel.LessThanOneDay => DeadlineUrgencyCode.LessThanOneDay,
        DeadlineUrgencyLevel.Overdue => DeadlineUrgencyCode.Overdue,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown deadline urgency.")
    };

    /// <summary>Maps an Application urgency code to its Domain enum.</summary>
    public static DeadlineUrgencyLevel ToDomain(DeadlineUrgencyCode value) => value switch
    {
        DeadlineUrgencyCode.None => DeadlineUrgencyLevel.None,
        DeadlineUrgencyCode.Neutral => DeadlineUrgencyLevel.Neutral,
        DeadlineUrgencyCode.MoreThanThreeDays => DeadlineUrgencyLevel.MoreThanThreeDays,
        DeadlineUrgencyCode.OneToThreeDays => DeadlineUrgencyLevel.OneToThreeDays,
        DeadlineUrgencyCode.LessThanOneDay => DeadlineUrgencyLevel.LessThanOneDay,
        DeadlineUrgencyCode.Overdue => DeadlineUrgencyLevel.Overdue,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown deadline urgency code.")
    };
}
