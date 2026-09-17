namespace ScheduleAssistant.Domain;

/// <summary>
/// Persisted task priority. Larger values represent higher priority.
/// </summary>
public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    Important = 2,
    UrgentAndImportant = 3
}
