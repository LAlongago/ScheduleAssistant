namespace ScheduleAssistant.Application.Tasks;

/// <summary>Application transport code for task priority.</summary>
public enum TaskPriorityCode
{
    Low = 0,
    Normal = 1,
    Important = 2,
    UrgentAndImportant = 3
}

/// <summary>Application transport code for the task workflow state.</summary>
public enum WorkflowStatusCode
{
    Pending = 0,
    InProgress = 1,
    Completed = 2
}

/// <summary>Application transport code for the calculated calendar display state.</summary>
public enum DisplayStatusCode
{
    Completed = 0,
    Overdue = 1,
    InProgress = 2,
    PlannedPast = 3,
    NotStarted = 4
}

/// <summary>Application transport code for the calculated deadline urgency.</summary>
public enum DeadlineUrgencyCode
{
    None = -1,
    Neutral = 0,
    MoreThanThreeDays = 1,
    OneToThreeDays = 2,
    LessThanOneDay = 3,
    Overdue = 4
}
