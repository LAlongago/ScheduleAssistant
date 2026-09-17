namespace ScheduleAssistant.Domain;

/// <summary>
/// Derived task status for presentation; it is not a persisted workflow state.
/// </summary>
public enum DisplayStatus
{
    Completed = 0,
    Overdue = 1,
    InProgress = 2,
    PlannedPast = 3,
    NotStarted = 4
}
