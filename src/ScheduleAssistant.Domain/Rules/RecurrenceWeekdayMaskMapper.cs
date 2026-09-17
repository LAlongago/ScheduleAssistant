namespace ScheduleAssistant.Domain;

/// <summary>
/// Centralizes the stable mapping between <see cref="DayOfWeek"/> and recurrence mask bits.
/// </summary>
public static class RecurrenceWeekdayMaskMapper
{
    /// <summary>
    /// Converts a .NET weekday to the domain mask bit. Monday is 1 and Sunday is 64.
    /// </summary>
    public static RecurrenceWeekdayMask FromDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => RecurrenceWeekdayMask.Monday,
            DayOfWeek.Tuesday => RecurrenceWeekdayMask.Tuesday,
            DayOfWeek.Wednesday => RecurrenceWeekdayMask.Wednesday,
            DayOfWeek.Thursday => RecurrenceWeekdayMask.Thursday,
            DayOfWeek.Friday => RecurrenceWeekdayMask.Friday,
            DayOfWeek.Saturday => RecurrenceWeekdayMask.Saturday,
            DayOfWeek.Sunday => RecurrenceWeekdayMask.Sunday,
            _ => throw new DomainValidationException("Unsupported day of week.", nameof(dayOfWeek))
        };
    }

    /// <summary>
    /// Tests whether a mask includes a .NET weekday.
    /// </summary>
    public static bool Contains(this RecurrenceWeekdayMask mask, DayOfWeek dayOfWeek)
    {
        return (mask & FromDayOfWeek(dayOfWeek)) != RecurrenceWeekdayMask.None;
    }
}
