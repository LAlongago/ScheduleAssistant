using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Recurrence;

public sealed partial class RecurrenceUseCases
{
    private static RecurrenceRule ToDomainRule(RecurrenceRuleDraft draft, DateOnly? effectiveDate = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var actualEffectiveDate = effectiveDate ?? draft.EffectiveDate;
        var weekdays = (RecurrenceWeekdayMask)(int)draft.Weekdays;
        return draft.Frequency switch
        {
            RecurrenceFrequencyCode.Daily => RecurrenceRule.CreateDaily(
                actualEffectiveDate,
                draft.TimeZoneId,
                draft.EndDate,
                draft.Interval),
            RecurrenceFrequencyCode.Weekly => RecurrenceRule.CreateWeekly(
                actualEffectiveDate,
                weekdays,
                draft.TimeZoneId,
                draft.EndDate,
                draft.Interval),
            RecurrenceFrequencyCode.Monthly => RecurrenceRule.CreateMonthly(
                actualEffectiveDate,
                draft.MonthDay ?? throw new ArgumentException("A monthly rule requires a month day.", nameof(draft)),
                draft.TimeZoneId,
                draft.EndDate,
                draft.Interval),
            RecurrenceFrequencyCode.Yearly => RecurrenceRule.CreateYearly(
                actualEffectiveDate,
                draft.YearMonth ?? throw new ArgumentException("A yearly rule requires a month.", nameof(draft)),
                draft.YearDay ?? throw new ArgumentException("A yearly rule requires a day.", nameof(draft)),
                draft.TimeZoneId,
                draft.EndDate,
                draft.Interval),
            _ => throw new ArgumentOutOfRangeException(nameof(draft), draft.Frequency, "Unknown recurrence frequency.")
        };
    }

    private static RecurrenceRule ToDomainRule(RecurrenceRule rule, DateOnly? endDate)
    {
        return new RecurrenceRule(
            rule.Frequency,
            rule.EffectiveDate,
            rule.TimeZoneId,
            endDate,
            rule.Weekdays,
            rule.MonthDay,
            rule.YearMonth,
            rule.YearDay,
            rule.Interval);
    }

    private static RecurrenceSeriesDto ToDto(RecurrenceSeries series)
    {
        return new RecurrenceSeriesDto(
            series.Id,
            series.Title,
            series.CategoryId,
            TaskContractMapper.ToCode(series.Priority),
            new RecurrenceRuleDto(
                ToCode(series.Rule.Frequency),
                series.Rule.EffectiveDate,
                series.Rule.TimeZoneId,
                series.Rule.EndDate,
                (RecurrenceWeekdayCode)(int)series.Rule.Weekdays,
                series.Rule.MonthDay,
                series.Rule.YearMonth,
                series.Rule.YearDay,
                series.Rule.Interval),
            series.PlannedStart,
            series.PlannedEnd,
            series.Location,
            series.Description,
            series.Materials,
            series.Notes,
            series.IsEnabled,
            series.CreatedAtUtc,
            series.UpdatedAtUtc,
            series.Version);
    }

    private static RecurrenceFrequencyCode ToCode(RecurrenceFrequency frequency)
    {
        return frequency switch
        {
            RecurrenceFrequency.Daily => RecurrenceFrequencyCode.Daily,
            RecurrenceFrequency.Weekly => RecurrenceFrequencyCode.Weekly,
            RecurrenceFrequency.Monthly => RecurrenceFrequencyCode.Monthly,
            RecurrenceFrequency.Yearly => RecurrenceFrequencyCode.Yearly,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unknown recurrence frequency.")
        };
    }

    private static ApplicationError? ValidateCategory(Category? category)
    {
        if (category is null)
        {
            return NotFound("Category.NotFound", "The selected category could not be found.");
        }

        return category.IsArchived
            ? ApplicationErrorMapper.Validation("Category.Archived", "The selected category is archived.")
            : null;
    }

    private static ApplicationError? ValidateExpectedVersion(RecurrenceSeries series, long expectedVersion)
    {
        return series.Version == expectedVersion
            ? null
            : new ApplicationError(
                ApplicationErrorKind.Conflict,
                "Persistence.Conflict",
                "The item was changed elsewhere. Reload it and try again.");
    }

    private static ApplicationError NotFound(string code, string message = "The requested recurrence series could not be found.")
    {
        return new ApplicationError(ApplicationErrorKind.NotFound, code, message);
    }

    private static DateOnly[] AffectedDates(
        IEnumerable<TaskItem> deleted,
        IEnumerable<MaterializedRecurrenceTask> inserted,
        DateOnly? anchor = null)
    {
        var dates = new HashSet<DateOnly>();
        if (anchor.HasValue)
        {
            dates.Add(anchor.Value);
        }

        foreach (var task in deleted)
        {
            if (task.OccurrenceDate is not null)
            {
                dates.Add(task.OccurrenceDate.Date);
            }

            if (task.PlannedDate.HasValue)
            {
                dates.Add(task.PlannedDate.Value);
            }
        }

        foreach (var task in inserted)
        {
            dates.Add(task.OccurrenceDate);
        }

        return dates.OrderBy(date => date).ToArray();
    }

    private static DateOnly[] AffectedDates(DateOnly occurrenceDate)
    {
        return new[] { occurrenceDate };
    }
}
