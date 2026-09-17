namespace ScheduleAssistant.Domain;

/// <summary>
/// Derived deadline urgency. <see cref="None"/> is distinct from neutral level zero.
/// </summary>
public enum DeadlineUrgencyLevel
{
    None = -1,
    Neutral = 0,
    MoreThanThreeDays = 1,
    OneToThreeDays = 2,
    LessThanOneDay = 3,
    Overdue = 4
}
