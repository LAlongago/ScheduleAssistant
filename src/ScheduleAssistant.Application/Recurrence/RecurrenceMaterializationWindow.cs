namespace ScheduleAssistant.Application.Recurrence;

/// <summary>An inclusive local-date window used by recurrence materialization.</summary>
public readonly record struct RecurrenceMaterializationWindow
{
    /// <summary>Initializes a validated inclusive window.</summary>
    public RecurrenceMaterializationWindow(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentException("The materialization end date must not be earlier than its start date.", nameof(toDate));
        }

        FromDate = fromDate;
        ToDate = toDate;
    }

    /// <summary>Gets the inclusive start date.</summary>
    public DateOnly FromDate { get; }

    /// <summary>Gets the inclusive end date.</summary>
    public DateOnly ToDate { get; }

    /// <summary>Creates the product default window from the provider's local calendar date.</summary>
    public static RecurrenceMaterializationWindow CreateDefault(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var localDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeProvider.LocalTimeZone).DateTime);
        return new RecurrenceMaterializationWindow(localDate.AddDays(-31), localDate.AddDays(400));
    }
}
