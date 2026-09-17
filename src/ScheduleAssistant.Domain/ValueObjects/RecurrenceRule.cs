namespace ScheduleAssistant.Domain;

/// <summary>
/// A validated V1 local-date recurrence rule. The rule is the sole owner of its effective range and time-zone ID.
/// </summary>
public sealed class RecurrenceRule : IEquatable<RecurrenceRule>
{
    private const RecurrenceWeekdayMask DefinedWeekdayBits = RecurrenceWeekdayMask.All;

    /// <summary>
    /// Initializes a recurrence rule. Query ranges are inclusive when used by <see cref="OccurrenceDateCalculator"/>.
    /// </summary>
    public RecurrenceRule(
        RecurrenceFrequency frequency,
        DateOnly effectiveDate,
        string timeZoneId,
        DateOnly? endDate = null,
        RecurrenceWeekdayMask weekdays = RecurrenceWeekdayMask.None,
        int? monthDay = null,
        int? yearMonth = null,
        int? yearDay = null,
        int interval = 1)
    {
        Frequency = DomainValidation.RequireDefinedEnum(frequency, nameof(frequency));
        EffectiveDate = effectiveDate;
        EndDate = endDate;
        TimeZoneId = DomainValidation.NormalizeTimeZoneId(timeZoneId);
        Interval = interval;
        Weekdays = weekdays;
        MonthDay = monthDay;
        YearMonth = yearMonth;
        YearDay = yearDay;

        if (endDate.HasValue && endDate.Value < effectiveDate)
        {
            throw new DomainValidationException("Recurrence end date must not be earlier than its effective date.", nameof(endDate));
        }

        if (interval != 1)
        {
            throw new DomainValidationException("V1 recurrence rules support only an interval of one.", nameof(interval));
        }

        if ((weekdays & ~DefinedWeekdayBits) != RecurrenceWeekdayMask.None)
        {
            throw new DomainValidationException("Weekly recurrence contains an undefined weekday bit.", nameof(weekdays));
        }

        ValidatePattern();
    }

    /// <summary>
    /// Creates a daily rule.
    /// </summary>
    public static RecurrenceRule CreateDaily(DateOnly effectiveDate, string timeZoneId, DateOnly? endDate = null, int interval = 1)
    {
        return new RecurrenceRule(RecurrenceFrequency.Daily, effectiveDate, timeZoneId, endDate, interval: interval);
    }

    /// <summary>
    /// Creates a weekly rule for one or more weekdays.
    /// </summary>
    public static RecurrenceRule CreateWeekly(
        DateOnly effectiveDate,
        RecurrenceWeekdayMask weekdays,
        string timeZoneId,
        DateOnly? endDate = null,
        int interval = 1)
    {
        return new RecurrenceRule(
            RecurrenceFrequency.Weekly,
            effectiveDate,
            timeZoneId,
            endDate,
            weekdays,
            interval: interval);
    }

    /// <summary>
    /// Creates a monthly rule. Days 29, 30 and 31 clamp to the last day of shorter months.
    /// </summary>
    public static RecurrenceRule CreateMonthly(DateOnly effectiveDate, int monthDay, string timeZoneId, DateOnly? endDate = null, int interval = 1)
    {
        return new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            effectiveDate,
            timeZoneId,
            endDate,
            monthDay: monthDay,
            interval: interval);
    }

    /// <summary>
    /// Creates a yearly rule. February 29 clamps to February's last day in non-leap years.
    /// </summary>
    public static RecurrenceRule CreateYearly(
        DateOnly effectiveDate,
        int yearMonth,
        int yearDay,
        string timeZoneId,
        DateOnly? endDate = null,
        int interval = 1)
    {
        return new RecurrenceRule(
            RecurrenceFrequency.Yearly,
            effectiveDate,
            timeZoneId,
            endDate,
            yearMonth: yearMonth,
            yearDay: yearDay,
            interval: interval);
    }

    /// <summary>
    /// Gets the recurrence frequency.
    /// </summary>
    public RecurrenceFrequency Frequency { get; }

    /// <summary>
    /// Gets the first date on which the rule may produce an occurrence.
    /// </summary>
    public DateOnly EffectiveDate { get; }

    /// <summary>
    /// Gets the inclusive last date, or <see langword="null"/> for an open-ended rule.
    /// </summary>
    public DateOnly? EndDate { get; }

    /// <summary>
    /// Gets the Windows time-zone ID associated with the local-date rule.
    /// No time-zone conversion is performed by this project.
    /// </summary>
    public string TimeZoneId { get; }

    /// <summary>
    /// Gets the fixed recurrence interval, always one in V1.
    /// </summary>
    public int Interval { get; }

    /// <summary>
    /// Gets the selected weekdays for a weekly rule.
    /// </summary>
    public RecurrenceWeekdayMask Weekdays { get; }

    /// <summary>
    /// Gets the selected day of month for a monthly rule.
    /// </summary>
    public int? MonthDay { get; }

    /// <summary>
    /// Gets the selected month for a yearly rule.
    /// </summary>
    public int? YearMonth { get; }

    /// <summary>
    /// Gets the selected day of month for a yearly rule.
    /// </summary>
    public int? YearDay { get; }

    /// <inheritdoc />
    public bool Equals(RecurrenceRule? other)
    {
        return other is not null
            && Frequency == other.Frequency
            && EffectiveDate == other.EffectiveDate
            && EndDate == other.EndDate
            && string.Equals(TimeZoneId, other.TimeZoneId, StringComparison.Ordinal)
            && Interval == other.Interval
            && Weekdays == other.Weekdays
            && MonthDay == other.MonthDay
            && YearMonth == other.YearMonth
            && YearDay == other.YearDay;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as RecurrenceRule);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Frequency);
        hash.Add(EffectiveDate);
        hash.Add(EndDate);
        hash.Add(TimeZoneId, StringComparer.Ordinal);
        hash.Add(Interval);
        hash.Add(Weekdays);
        hash.Add(MonthDay);
        hash.Add(YearMonth);
        hash.Add(YearDay);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Compares two recurrence rules by value.
    /// </summary>
    public static bool operator ==(RecurrenceRule? left, RecurrenceRule? right)
    {
        return ReferenceEquals(left, right) || left is not null && left.Equals(right);
    }

    /// <summary>
    /// Compares two recurrence rules by value.
    /// </summary>
    public static bool operator !=(RecurrenceRule? left, RecurrenceRule? right)
    {
        return !(left == right);
    }

    private void ValidatePattern()
    {
        switch (Frequency)
        {
            case RecurrenceFrequency.Daily:
                Ensure(
                    Weekdays == RecurrenceWeekdayMask.None && MonthDay is null && YearMonth is null && YearDay is null,
                    "Daily recurrence must not carry weekly, monthly or yearly pattern fields.");
                break;
            case RecurrenceFrequency.Weekly:
                Ensure(Weekdays != RecurrenceWeekdayMask.None, "Weekly recurrence must select at least one weekday.");
                Ensure(MonthDay is null && YearMonth is null && YearDay is null, "Weekly recurrence must not carry monthly or yearly pattern fields.");
                break;
            case RecurrenceFrequency.Monthly:
                Ensure(MonthDay is >= 1 and <= 31, "Monthly recurrence day must be between 1 and 31.");
                Ensure(Weekdays == RecurrenceWeekdayMask.None && YearMonth is null && YearDay is null, "Monthly recurrence must not carry other pattern fields.");
                break;
            case RecurrenceFrequency.Yearly:
                Ensure(Weekdays == RecurrenceWeekdayMask.None && MonthDay is null, "Yearly recurrence must not carry weekly or monthly pattern fields.");
                Ensure(YearMonth is >= 1 and <= 12, "Yearly recurrence month must be between 1 and 12.");
                Ensure(YearDay is >= 1 and <= 31, "Yearly recurrence day must be between 1 and 31.");

                if (YearMonth.HasValue && YearDay.HasValue)
                {
                    var maximumDay = DateTime.DaysInMonth(2000, YearMonth.Value);
                    var isClampedLeapDay = YearMonth.Value == 2 && YearDay.Value == 29;
                    Ensure(isClampedLeapDay || YearDay.Value <= maximumDay, "Yearly recurrence date is not a valid calendar date.");
                }

                break;
            default:
                throw new DomainValidationException("Unsupported recurrence frequency.", nameof(Frequency));
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new DomainValidationException(message);
        }
    }
}
