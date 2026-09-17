namespace ScheduleAssistant.Domain;

/// <summary>
/// Weekly recurrence mask. Monday is the least-significant weekday bit and Sunday is the highest.
/// </summary>
[Flags]
public enum RecurrenceWeekdayMask
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    All = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday
}
