namespace ScheduleAssistant.Domain;

/// <summary>
/// Persisted user workflow state for a task.
/// </summary>
public enum WorkflowStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2
}
