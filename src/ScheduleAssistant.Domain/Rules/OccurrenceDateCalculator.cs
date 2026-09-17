namespace ScheduleAssistant.Domain;

/// <summary>
/// Pure local-date recurrence calculation for the inclusive query window.
/// It never creates task identities, converts time zones, reads a clock, or persists data.
/// </summary>
public static class OccurrenceDateCalculator
{
    /// <summary>
    /// Returns sorted, distinct occurrence dates in the inclusive intersection of the query and rule ranges.
    /// </summary>
    public static IReadOnlyList<DateOnly> Calculate(RecurrenceRule rule, DateOnly queryStart, DateOnly queryEnd)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (queryStart > queryEnd)
        {
            throw new DomainValidationException("The recurrence query start date must not be later than its end date.", nameof(queryStart));
        }

        var start = LaterOf(queryStart, rule.EffectiveDate);
        var end = rule.EndDate.HasValue ? EarlierOf(queryEnd, rule.EndDate.Value) : queryEnd;
        if (start > end)
        {
            return Array.Empty<DateOnly>();
        }

        return rule.Frequency switch
        {
            RecurrenceFrequency.Daily => CalculateDaily(start, end),
            RecurrenceFrequency.Weekly => CalculateWeekly(rule.Weekdays, start, end),
            RecurrenceFrequency.Monthly => CalculateMonthly(rule.MonthDay!.Value, start, end),
            RecurrenceFrequency.Yearly => CalculateYearly(rule.YearMonth!.Value, rule.YearDay!.Value, start, end),
            _ => throw new DomainValidationException("Unsupported recurrence frequency.", nameof(rule))
        };
    }

    /// <summary>
    /// Returns the same calculation as <see cref="Calculate"/> wrapped in occurrence value objects.
    /// </summary>
    public static IReadOnlyList<OccurrenceDate> CalculateOccurrences(RecurrenceRule rule, DateOnly queryStart, DateOnly queryEnd)
    {
        return Calculate(rule, queryStart, queryEnd).Select(date => new OccurrenceDate(date)).ToArray();
    }

    /// <summary>
    /// Alias for callers that prefer the query terminology.
    /// </summary>
    public static IReadOnlyList<DateOnly> GetOccurrences(RecurrenceRule rule, DateOnly queryStart, DateOnly queryEnd)
    {
        return Calculate(rule, queryStart, queryEnd);
    }

    private static List<DateOnly> CalculateDaily(DateOnly start, DateOnly end)
    {
        var dates = new List<DateOnly>();
        var current = start;
        while (true)
        {
            dates.Add(current);
            if (!TryMoveToNextDay(current, end, out current))
            {
                break;
            }
        }

        return dates;
    }

    private static List<DateOnly> CalculateWeekly(RecurrenceWeekdayMask weekdays, DateOnly start, DateOnly end)
    {
        var dates = new List<DateOnly>();
        var current = start;
        while (true)
        {
            if (weekdays.Contains(current.DayOfWeek))
            {
                AddDistinct(dates, current);
            }

            if (!TryMoveToNextDay(current, end, out current))
            {
                break;
            }
        }

        return dates;
    }

    private static List<DateOnly> CalculateMonthly(int monthDay, DateOnly start, DateOnly end)
    {
        var dates = new List<DateOnly>();
        var year = start.Year;
        var month = start.Month;
        while (true)
        {
            var day = Math.Min(monthDay, DateTime.DaysInMonth(year, month));
            AddIfInRange(dates, new DateOnly(year, month, day), start, end);

            if (year == end.Year && month == end.Month)
            {
                break;
            }

            AdvanceMonth(ref year, ref month);
        }

        return dates;
    }

    private static List<DateOnly> CalculateYearly(int yearMonth, int yearDay, DateOnly start, DateOnly end)
    {
        var dates = new List<DateOnly>();
        var year = start.Year;
        while (true)
        {
            var day = yearMonth == 2 && yearDay == 29 && !DateTime.IsLeapYear(year) ? 28 : yearDay;
            AddIfInRange(dates, new DateOnly(year, yearMonth, day), start, end);

            if (year == end.Year)
            {
                break;
            }

            year++;
        }

        return dates;
    }

    private static void AddIfInRange(List<DateOnly> dates, DateOnly candidate, DateOnly start, DateOnly end)
    {
        if (candidate >= start && candidate <= end)
        {
            AddDistinct(dates, candidate);
        }
    }

    private static void AddDistinct(List<DateOnly> dates, DateOnly candidate)
    {
        if (dates.Count == 0 || dates[^1] != candidate)
        {
            dates.Add(candidate);
        }
    }

    private static bool TryMoveToNextDay(DateOnly current, DateOnly end, out DateOnly next)
    {
        if (current == end)
        {
            next = default;
            return false;
        }

        next = current.AddDays(1);
        return true;
    }

    private static void AdvanceMonth(ref int year, ref int month)
    {
        if (month == 12)
        {
            year++;
            month = 1;
        }
        else
        {
            month++;
        }
    }

    private static DateOnly LaterOf(DateOnly left, DateOnly right)
    {
        return left >= right ? left : right;
    }

    private static DateOnly EarlierOf(DateOnly left, DateOnly right)
    {
        return left <= right ? left : right;
    }
}
