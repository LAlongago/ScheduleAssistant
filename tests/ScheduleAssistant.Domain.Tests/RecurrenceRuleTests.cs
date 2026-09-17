using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Domain.Tests;

public sealed class RecurrenceRuleTests
{
    [Fact]
    public void StableEnums_ShouldUseTheSpecifiedValues()
    {
        Assert.Equal(0, (int)WorkflowStatus.Pending);
        Assert.Equal(1, (int)WorkflowStatus.InProgress);
        Assert.Equal(2, (int)WorkflowStatus.Completed);
        Assert.Equal(0, (int)TaskPriority.Low);
        Assert.Equal(1, (int)TaskPriority.Normal);
        Assert.Equal(2, (int)TaskPriority.Important);
        Assert.Equal(3, (int)TaskPriority.UrgentAndImportant);
        Assert.Equal(0, (int)RecurrenceFrequency.Daily);
        Assert.Equal(1, (int)RecurrenceFrequency.Weekly);
        Assert.Equal(2, (int)RecurrenceFrequency.Monthly);
        Assert.Equal(3, (int)RecurrenceFrequency.Yearly);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, RecurrenceWeekdayMask.Monday)]
    [InlineData(DayOfWeek.Tuesday, RecurrenceWeekdayMask.Tuesday)]
    [InlineData(DayOfWeek.Wednesday, RecurrenceWeekdayMask.Wednesday)]
    [InlineData(DayOfWeek.Thursday, RecurrenceWeekdayMask.Thursday)]
    [InlineData(DayOfWeek.Friday, RecurrenceWeekdayMask.Friday)]
    [InlineData(DayOfWeek.Saturday, RecurrenceWeekdayMask.Saturday)]
    [InlineData(DayOfWeek.Sunday, RecurrenceWeekdayMask.Sunday)]
    public void WeekdayMaskMapper_ShouldUseMondayThroughSundayBits(DayOfWeek dayOfWeek, RecurrenceWeekdayMask expected)
    {
        Assert.Equal(expected, RecurrenceWeekdayMaskMapper.FromDayOfWeek(dayOfWeek));
    }

    [Fact]
    public void CreateWeekly_WhenMaskIsEmptyOrHasUndefinedBits_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => RecurrenceRule.CreateWeekly(
            new DateOnly(2026, 1, 1),
            RecurrenceWeekdayMask.None,
            "China Standard Time"));
        Assert.Throws<DomainValidationException>(() => RecurrenceRule.CreateWeekly(
            new DateOnly(2026, 1, 1),
            (RecurrenceWeekdayMask)128,
            "China Standard Time"));
    }

    [Fact]
    public void Create_WhenIntervalIsNotOne_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => RecurrenceRule.CreateDaily(
            new DateOnly(2026, 1, 1),
            "China Standard Time",
            interval: 2));
    }

    [Fact]
    public void Create_WhenEffectiveRangeIsInverted_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => RecurrenceRule.CreateDaily(
            new DateOnly(2026, 1, 2),
            "China Standard Time",
            new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Create_WhenPatternFieldsDoNotMatchFrequency_ShouldRejectThem()
    {
        Assert.Throws<DomainValidationException>(() => new RecurrenceRule(
            RecurrenceFrequency.Daily,
            new DateOnly(2026, 1, 1),
            "China Standard Time",
            weekdays: RecurrenceWeekdayMask.Monday));
        Assert.Throws<DomainValidationException>(() => new RecurrenceRule(
            RecurrenceFrequency.Monthly,
            new DateOnly(2026, 1, 1),
            "China Standard Time",
            monthDay: 1,
            yearMonth: 1));
        Assert.Throws<DomainValidationException>(() => new RecurrenceRule(
            (RecurrenceFrequency)99,
            new DateOnly(2026, 1, 1),
            "China Standard Time"));
    }

    [Fact]
    public void CreateYearly_WhenDateIsInvalidExceptFebruary29_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => RecurrenceRule.CreateYearly(
            new DateOnly(2026, 1, 1),
            4,
            31,
            "China Standard Time"));
        Assert.Throws<DomainValidationException>(() => RecurrenceRule.CreateYearly(
            new DateOnly(2026, 1, 1),
            2,
            30,
            "China Standard Time"));

        var leapDay = RecurrenceRule.CreateYearly(
            new DateOnly(2026, 1, 1),
            2,
            29,
            "Unrecognized Windows Zone Id");

        Assert.Equal(2, leapDay.YearMonth);
        Assert.Equal(29, leapDay.YearDay);
        Assert.Equal("Unrecognized Windows Zone Id", leapDay.TimeZoneId);
    }

    [Fact]
    public void CalculateDaily_ShouldUseInclusiveWindowAndRuleIntersection()
    {
        var rule = RecurrenceRule.CreateDaily(
            new DateOnly(2026, 1, 3),
            "China Standard Time",
            new DateOnly(2026, 1, 5));

        var dates = OccurrenceDateCalculator.Calculate(
            rule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 10));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 1, 3),
                new DateOnly(2026, 1, 4),
                new DateOnly(2026, 1, 5)
            },
            dates);
    }

    [Fact]
    public void CalculateDaily_WhenQueryDoesNotIntersectRule_ShouldReturnEmpty()
    {
        var rule = RecurrenceRule.CreateDaily(new DateOnly(2026, 2, 1), "China Standard Time");

        Assert.Empty(OccurrenceDateCalculator.Calculate(
            rule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void CalculateWeekly_WhenSingleDaySelected_ShouldSupportCrossYearQueries()
    {
        var rule = RecurrenceRule.CreateWeekly(
            new DateOnly(2025, 12, 29),
            RecurrenceWeekdayMask.Monday,
            "China Standard Time");

        var dates = OccurrenceDateCalculator.Calculate(
            rule,
            new DateOnly(2025, 12, 28),
            new DateOnly(2026, 1, 12));

        Assert.Equal(
            new[]
            {
                new DateOnly(2025, 12, 29),
                new DateOnly(2026, 1, 5),
                new DateOnly(2026, 1, 12)
            },
            dates);
    }

    [Fact]
    public void CalculateWeekly_WhenMultipleDaysSelected_ShouldReturnSortedDistinctDates()
    {
        var rule = RecurrenceRule.CreateWeekly(
            new DateOnly(2026, 1, 1),
            RecurrenceWeekdayMask.Monday | RecurrenceWeekdayMask.Wednesday | RecurrenceWeekdayMask.Friday,
            "China Standard Time",
            new DateOnly(2026, 1, 11));

        var dates = OccurrenceDateCalculator.Calculate(
            rule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 11));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 1, 2),
                new DateOnly(2026, 1, 5),
                new DateOnly(2026, 1, 7),
                new DateOnly(2026, 1, 9)
            },
            dates);
        Assert.Equal(dates.Count, dates.Distinct().Count());
    }

    [Theory]
    [InlineData(29, 29, 29, 29, 29)]
    [InlineData(30, 30, 29, 30, 30)]
    [InlineData(31, 31, 29, 31, 30)]
    public void CalculateMonthly_WhenMonthDoesNotHaveSelectedDay_ShouldClampToLastDay(
        int monthDay,
        int januaryDay,
        int februaryDay,
        int marchDay,
        int aprilDay)
    {
        var rule = RecurrenceRule.CreateMonthly(new DateOnly(2024, 1, 1), monthDay, "China Standard Time");

        var dates = OccurrenceDateCalculator.Calculate(
            rule,
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 4, 30));

        Assert.Equal(
            new[]
            {
                new DateOnly(2024, 1, januaryDay),
                new DateOnly(2024, 2, februaryDay),
                new DateOnly(2024, 3, marchDay),
                new DateOnly(2024, 4, aprilDay)
            },
            dates);
    }

    [Fact]
    public void CalculateMonthly_WhenNonLeapFebruaryIsEncountered_ShouldClampToFebruary28()
    {
        var rule = RecurrenceRule.CreateMonthly(new DateOnly(2023, 1, 1), 31, "China Standard Time");

        Assert.Equal(
            new[]
            {
                new DateOnly(2023, 1, 31),
                new DateOnly(2023, 2, 28),
                new DateOnly(2023, 3, 31)
            },
            OccurrenceDateCalculator.Calculate(rule, new DateOnly(2023, 1, 1), new DateOnly(2023, 3, 31)));
    }

    [Fact]
    public void CalculateYearly_WhenFebruary29IsUsed_ShouldClampOnlyNonLeapYears()
    {
        var rule = RecurrenceRule.CreateYearly(new DateOnly(2023, 1, 1), 2, 29, "China Standard Time");

        Assert.Equal(
            new[]
            {
                new DateOnly(2023, 2, 28),
                new DateOnly(2024, 2, 29),
                new DateOnly(2025, 2, 28)
            },
            OccurrenceDateCalculator.Calculate(rule, new DateOnly(2023, 1, 1), new DateOnly(2025, 12, 31)));
    }

    [Fact]
    public void CalculateOccurrences_ShouldWrapDateOnlyResultsWithoutChangingValues()
    {
        var rule = RecurrenceRule.CreateDaily(new DateOnly(2026, 1, 2), "China Standard Time");

        var occurrences = OccurrenceDateCalculator.CalculateOccurrences(
            rule,
            new DateOnly(2026, 1, 2),
            new DateOnly(2026, 1, 3));

        Assert.Equal(new OccurrenceDate(new DateOnly(2026, 1, 2)), occurrences[0]);
        Assert.Equal(new OccurrenceDate(new DateOnly(2026, 1, 3)), occurrences[1]);
    }

    [Fact]
    public void Calculate_WhenQueryStartIsAfterQueryEnd_ShouldRejectIt()
    {
        var rule = RecurrenceRule.CreateDaily(new DateOnly(2026, 1, 1), "China Standard Time");

        Assert.Throws<DomainValidationException>(() => OccurrenceDateCalculator.Calculate(
            rule,
            new DateOnly(2026, 1, 2),
            new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Calculate_WhenUsingDateOnlyExtremes_ShouldStopBeforeOverflow()
    {
        var minimumDates = OccurrenceDateCalculator.Calculate(
            RecurrenceRule.CreateDaily(DateOnly.MinValue, "UTC"),
            DateOnly.MinValue,
            DateOnly.MinValue.AddDays(1));
        var maximumDates = OccurrenceDateCalculator.Calculate(
            RecurrenceRule.CreateDaily(DateOnly.MaxValue.AddDays(-1), "UTC"),
            DateOnly.MaxValue.AddDays(-1),
            DateOnly.MaxValue);
        var maximumMonth = OccurrenceDateCalculator.Calculate(
            RecurrenceRule.CreateMonthly(DateOnly.MaxValue, 31, "UTC"),
            DateOnly.MaxValue,
            DateOnly.MaxValue);

        Assert.Equal(new[] { DateOnly.MinValue, DateOnly.MinValue.AddDays(1) }, minimumDates);
        Assert.Equal(new[] { DateOnly.MaxValue.AddDays(-1), DateOnly.MaxValue }, maximumDates);
        Assert.Equal(new[] { DateOnly.MaxValue }, maximumMonth);
    }
}
