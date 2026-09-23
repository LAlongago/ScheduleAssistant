using ScheduleAssistant.Application.Recurrence;

namespace ScheduleAssistant.Presentation.ViewModels;

public sealed partial class TaskEditorViewModel
{
    private void ValidateRecurrenceForm(IDictionary<string, List<string>> errors)
    {
        if (!IsRecurrenceSubmission)
        {
            return;
        }

        if (!Enum.IsDefined(SelectedRecurrenceFrequency))
        {
            AddError(errors, nameof(SelectedRecurrenceFrequency), "请选择有效的周期频率。");
        }

        var effectiveDate = IsEditingRecurrenceSeries
            ? ToDateOnly(ApplyFromDateValue)
            : ToDateOnly(RecurrenceEffectiveDateValue);
        if (!effectiveDate.HasValue)
        {
            AddError(
                errors,
                IsEditingRecurrenceSeries ? nameof(ApplyFromDateValue) : nameof(RecurrenceEffectiveDateValue),
                "请选择周期生效日期。");
        }
        else if (IsEditingRecurrenceSeries
                 && _loadedTask?.OccurrenceDate is DateOnly occurrenceDate
                 && effectiveDate.Value < occurrenceDate)
        {
            AddError(errors, nameof(ApplyFromDateValue), "ApplyFromDate 不能早于当前实例日期。");
        }

        var endDate = ToDateOnly(RecurrenceEndDateValue);
        if (endDate.HasValue && effectiveDate.HasValue && endDate.Value < effectiveDate.Value)
        {
            AddError(errors, nameof(RecurrenceEndDateValue), "结束日期不能早于生效日期。");
        }

        if (string.IsNullOrWhiteSpace(RecurrenceTimeZoneId)
            || !RecurrenceTimeZoneOptions.Any(option =>
                string.Equals(option.Id, RecurrenceTimeZoneId, StringComparison.OrdinalIgnoreCase)))
        {
            AddError(errors, nameof(RecurrenceTimeZoneId), "请选择有效的时区。");
        }
        else
        {
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(RecurrenceTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                AddError(errors, nameof(RecurrenceTimeZoneId), "所选时区在此设备上不可用。");
            }
            catch (InvalidTimeZoneException)
            {
                AddError(errors, nameof(RecurrenceTimeZoneId), "所选时区数据无效，请重新选择。");
            }
            catch (ArgumentException)
            {
                AddError(errors, nameof(RecurrenceTimeZoneId), "请选择有效的时区。");
            }
        }

        if (SelectedRecurrenceFrequency == RecurrenceFrequencyCode.Weekly)
        {
            if (SelectedWeekdays == RecurrenceWeekdayCode.None)
            {
                AddError(errors, nameof(SelectedWeekdays), "每周规则至少选择一天。");
            }
            else if ((SelectedWeekdays & ~RecurrenceWeekdayCode.All) != RecurrenceWeekdayCode.None)
            {
                AddError(errors, nameof(SelectedWeekdays), "每周规则包含无效的星期选项。");
            }
        }

        if (SelectedRecurrenceFrequency == RecurrenceFrequencyCode.Monthly
            && MonthlyDay is < 1 or > 31)
        {
            AddError(errors, nameof(MonthlyDay), "每月日期必须在 1 到 31 之间。");
        }

        if (SelectedRecurrenceFrequency == RecurrenceFrequencyCode.Yearly
            && (YearMonth is < 1 or > 12
                || !RecurrenceYearDayOptions.Contains(YearDay)))
        {
            AddError(errors, nameof(YearDay), "请选择有效的年、月、日组合。");
        }
    }

    private bool TryBuildRecurrenceDraft(out RecurrenceSeriesDraft draft)
    {
        draft = null!;
        ValidateForm();
        if (HasErrors || _recurrenceUseCases is null)
        {
            return false;
        }

        var effectiveDate = IsEditingRecurrenceSeries
            ? ToDateOnly(ApplyFromDateValue)!.Value
            : ToDateOnly(RecurrenceEffectiveDateValue)!.Value;
        var rule = new RecurrenceRuleDraft(
            SelectedRecurrenceFrequency,
            effectiveDate,
            RecurrenceTimeZoneId,
            ToDateOnly(RecurrenceEndDateValue),
            SelectedRecurrenceFrequency == RecurrenceFrequencyCode.Weekly
                ? SelectedWeekdays
                : RecurrenceWeekdayCode.None,
            SelectedRecurrenceFrequency == RecurrenceFrequencyCode.Monthly ? MonthlyDay : null,
            SelectedRecurrenceFrequency == RecurrenceFrequencyCode.Yearly ? YearMonth : null,
            SelectedRecurrenceFrequency == RecurrenceFrequencyCode.Yearly ? YearDay : null,
            Interval: 1);
        draft = new RecurrenceSeriesDraft(
            Title,
            SelectedCategoryId,
            SelectedPriority,
            rule,
            ParseTimeOrNull(PlannedStartText),
            ParseTimeOrNull(PlannedEndText),
            NormalizeOptional(Location),
            NormalizeOptional(Description),
            NormalizeOptional(Materials),
            NormalizeOptional(Notes),
            IsEnabled: _loadedRecurrenceSeries?.IsEnabled ?? true);
        return true;
    }

    private static List<RecurrenceTimeZoneOption> LoadTimeZoneOptions(string selectedId)
    {
        var options = TimeZoneInfo.GetSystemTimeZones()
            .Select(timeZone => new RecurrenceTimeZoneOption(timeZone.Id, $"{timeZone.DisplayName} ({timeZone.Id})"))
            .OrderBy(option => option.Label, StringComparer.CurrentCulture)
            .ToList();
        if (!options.Any(option => string.Equals(option.Id, selectedId, StringComparison.OrdinalIgnoreCase)))
        {
            options.Insert(0, new RecurrenceTimeZoneOption(selectedId, selectedId));
        }

        return options;
    }
}
