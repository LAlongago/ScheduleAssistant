using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScheduleAssistant.Application.Recurrence;
using ScheduleAssistant.Application.Tasks;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>A localized recurrence frequency choice backed by the Application contract.</summary>
public sealed record RecurrenceFrequencyOption(RecurrenceFrequencyCode Value, string Label);

/// <summary>A localized recurrence time-zone choice backed by a system time-zone identifier.</summary>
public sealed record RecurrenceTimeZoneOption(string Id, string Label);

/// <summary>A localized month choice for an annual recurrence rule.</summary>
public sealed record RecurrenceMonthOption(int Value, string Label);

/// <summary>One selectable weekday in the recurrence editor.</summary>
public sealed class RecurrenceWeekdayOption : ObservableObject
{
    private bool _isSelected;

    internal RecurrenceWeekdayOption(RecurrenceWeekdayCode value, string label)
    {
        Value = value;
        Label = label;
    }

    /// <summary>Gets the Application weekday flag represented by this choice.</summary>
    public RecurrenceWeekdayCode Value { get; }

    /// <summary>Gets the localized weekday name.</summary>
    public string Label { get; }

    /// <summary>Gets or sets whether this weekday is part of the weekly rule.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    internal event EventHandler? SelectionChanged;
}

public sealed partial class TaskEditorViewModel
{
    private readonly IRecurrenceUseCases? _recurrenceUseCases;
    private readonly ObservableCollection<RecurrenceWeekdayOption> _weekdayOptions = [];
    private readonly IReadOnlyList<RecurrenceFrequencyOption> _recurrenceFrequencyOptions =
    [
        new(RecurrenceFrequencyCode.Daily, "每天"),
        new(RecurrenceFrequencyCode.Weekly, "每周"),
        new(RecurrenceFrequencyCode.Monthly, "每月"),
        new(RecurrenceFrequencyCode.Yearly, "每年")
    ];
    private readonly IReadOnlyList<RecurrenceMonthOption> _recurrenceMonthOptions =
        Enumerable.Range(1, 12)
            .Select(month => new RecurrenceMonthOption(month, $"{month} 月"))
            .ToArray();
    private IReadOnlyList<RecurrenceTimeZoneOption> _recurrenceTimeZoneOptions =
        Array.Empty<RecurrenceTimeZoneOption>();
    private RecurrenceSeriesDto? _loadedRecurrenceSeries;
    private TaskDto? _loadedTask;
    private bool _recurrenceEnabled;
    private bool _isEditingRecurrenceSeries;
    private bool _recurrenceOperationCommitted;
    private RecurrenceFrequencyCode _selectedRecurrenceFrequency = RecurrenceFrequencyCode.Daily;
    private DateTime? _recurrenceEffectiveDateValue;
    private DateTime? _recurrenceEndDateValue;
    private DateTime? _applyFromDateValue;
    private DateTime? _deleteFromDateValue;
    private string _recurrenceTimeZoneId = TimeZoneInfo.Local.Id;
    private int _monthlyDay = 1;
    private int _yearMonth = 1;
    private int _yearDay = 1;
    private string? _recurrenceModeError;
    private string? _recurrenceLoadMessage;

    private void InitializeRecurrenceState()
    {
        foreach (var (value, label) in new[]
        {
            (RecurrenceWeekdayCode.Monday, "周一"),
            (RecurrenceWeekdayCode.Tuesday, "周二"),
            (RecurrenceWeekdayCode.Wednesday, "周三"),
            (RecurrenceWeekdayCode.Thursday, "周四"),
            (RecurrenceWeekdayCode.Friday, "周五"),
            (RecurrenceWeekdayCode.Saturday, "周六"),
            (RecurrenceWeekdayCode.Sunday, "周日")
        })
        {
            var option = new RecurrenceWeekdayOption(value, label);
            option.SelectionChanged += OnWeekdaySelectionChanged;
            _weekdayOptions.Add(option);
        }

        _recurrenceTimeZoneOptions = LoadTimeZoneOptions(_recurrenceTimeZoneId);
        RecurrenceWeekdayOptions = new ReadOnlyObservableCollection<RecurrenceWeekdayOption>(_weekdayOptions);
        RecurrenceFrequencyOptions = _recurrenceFrequencyOptions;
        RecurrenceMonthOptions = _recurrenceMonthOptions;
        RecurrenceDayOptions = Enumerable.Range(1, 31).ToArray();
        RecurrenceTimeZoneOptions = _recurrenceTimeZoneOptions;
        ClearRecurrenceEndDateCommand = new RelayCommand(ClearRecurrenceEndDate);
        EditRecurrenceSeriesCommand = new AsyncRelayCommand(EditRecurrenceSeriesAsync, CanEditRecurrenceSeries);
        ReturnToOccurrenceCommand = new AsyncRelayCommand(ReturnToOccurrenceAsync, CanReturnToOccurrence);
        DeleteOccurrenceCommand = new AsyncRelayCommand(DeleteOccurrenceAsync, CanDeleteOccurrence);
        DeleteFutureOccurrencesCommand = new AsyncRelayCommand(DeleteFutureOccurrencesAsync, CanDeleteFutureOccurrences);

        var today = TodayLocalDate;
        var initialDate = _request.PrefilledPlannedDate ?? today;
        _recurrenceEffectiveDateValue = initialDate.ToDateTime(TimeOnly.MinValue);
        _monthlyDay = initialDate.Day;
        _yearMonth = initialDate.Month;
        _yearDay = initialDate.Day;
    }

    /// <summary>Gets whether the Application recurrence use cases are available.</summary>
    public bool IsRecurrenceFeatureAvailable => _recurrenceUseCases is not null;

    /// <summary>Gets whether this editor is creating a recurrence series.</summary>
    public bool IsRecurringCreationMode => IsCreateMode && _recurrenceEnabled;

    /// <summary>Gets whether the loaded task is one materialized recurrence occurrence.</summary>
    public bool IsRecurrenceOccurrence =>
        _loadedTask is not null && _loadedTask.SeriesId.HasValue && _loadedTask.OccurrenceDate.HasValue;

    /// <summary>Gets whether the current form edits a series and its unfinished future occurrences.</summary>
    public bool IsEditingRecurrenceSeries => _isEditingRecurrenceSeries;

    /// <summary>Gets whether recurrence actions apply to the current instance instead of the series.</summary>
    public bool IsNotEditingRecurrenceSeries => !IsEditingRecurrenceSeries;

    /// <summary>Gets whether recurrence rule fields are currently being edited.</summary>
    public bool IsRecurrenceConfigurationMode => IsRecurringCreationMode || IsEditingRecurrenceSeries;

    /// <summary>Gets whether task-only Deadline, reminder, planned-date, and attachment fields can be edited.</summary>
    public bool IsTaskOnlyDataEnabled => !IsRecurrenceConfigurationMode;

    /// <summary>Gets whether the recurrence toggle can be used in this editor.</summary>
    public bool CanCreateRecurrenceSeries =>
        IsCreateMode && IsRecurrenceFeatureAvailable && !IsBusy && !_recurrenceOperationCommitted;

    /// <summary>Gets whether a recurrence feature explanation is needed.</summary>
    public bool IsRecurrenceUnavailable => !IsRecurrenceFeatureAvailable;

    /// <summary>Gets whether an existing ordinary task is outside the recurrence-series editor.</summary>
    public bool IsOrdinaryTaskEdit => IsEditMode && !IsRecurrenceOccurrence;

    /// <summary>Gets or sets whether a new task should be saved as a recurrence series.</summary>
    public bool IsRecurrenceEnabled
    {
        get => _recurrenceEnabled;
        set
        {
            if (_recurrenceEnabled == value)
            {
                return;
            }

            if (value && !CanCreateRecurrenceSeries)
            {
                return;
            }

            if (value && (HasDeadline || HasAttachments))
            {
                RecurrenceModeError = "周期系列不支持 Deadline、提醒或附件。请先取消已选 Deadline 和提醒，并移除附件后再开启周期。";
                OnPropertyChanged();
                return;
            }

            if (!SetEditorProperty(ref _recurrenceEnabled, value))
            {
                return;
            }

            RecurrenceModeError = null;
            if (value && RecurrenceEffectiveDateValue is null)
            {
                RecurrenceEffectiveDateValue = (PlannedDate ?? TodayLocalDate).ToDateTime(TimeOnly.MinValue);
            }

            NotifyRecurrenceStateChanged();
        }
    }

    /// <summary>Gets the supported recurrence frequency choices.</summary>
    public IReadOnlyList<RecurrenceFrequencyOption> RecurrenceFrequencyOptions { get; private set; } =
        Array.Empty<RecurrenceFrequencyOption>();

    /// <summary>Gets or sets the selected recurrence frequency.</summary>
    public RecurrenceFrequencyCode SelectedRecurrenceFrequency
    {
        get => _selectedRecurrenceFrequency;
        set
        {
            if (SetEditorProperty(ref _selectedRecurrenceFrequency, value))
            {
                OnPropertyChanged(nameof(IsWeeklyRule));
                OnPropertyChanged(nameof(IsMonthlyRule));
                OnPropertyChanged(nameof(IsYearlyRule));
                OnPropertyChanged(nameof(RecurrencePolicyNotice));
                OnPropertyChanged(nameof(HasRecurrencePolicyNotice));
            }
        }
    }

    /// <summary>Gets whether weekday choices are shown.</summary>
    public bool IsWeeklyRule => SelectedRecurrenceFrequency == RecurrenceFrequencyCode.Weekly;

    /// <summary>Gets whether a month-day choice is shown.</summary>
    public bool IsMonthlyRule => SelectedRecurrenceFrequency == RecurrenceFrequencyCode.Monthly;

    /// <summary>Gets whether a year month-day choice is shown.</summary>
    public bool IsYearlyRule => SelectedRecurrenceFrequency == RecurrenceFrequencyCode.Yearly;

    /// <summary>Gets the selected weekdays using Application contract flags.</summary>
    public RecurrenceWeekdayCode SelectedWeekdays => _weekdayOptions
        .Where(option => option.IsSelected)
        .Aggregate(RecurrenceWeekdayCode.None, (selected, option) => selected | option.Value);

    /// <summary>Gets the selectable weekdays for weekly rules.</summary>
    public ReadOnlyObservableCollection<RecurrenceWeekdayOption> RecurrenceWeekdayOptions { get; private set; } = null!;

    /// <summary>Gets the available day numbers from 1 through 31.</summary>
    public IReadOnlyList<int> RecurrenceDayOptions { get; private set; } = Array.Empty<int>();

    /// <summary>Gets the available months for yearly rules.</summary>
    public IReadOnlyList<RecurrenceMonthOption> RecurrenceMonthOptions { get; private set; } =
        Array.Empty<RecurrenceMonthOption>();

    /// <summary>Gets valid day numbers for the selected yearly month, allowing February 29.</summary>
    public IReadOnlyList<int> RecurrenceYearDayOptions =>
        Enumerable.Range(1, DateTime.DaysInMonth(2000, Math.Clamp(YearMonth, 1, 12))).ToArray();

    /// <summary>Gets or sets the monthly day number.</summary>
    public int MonthlyDay
    {
        get => _monthlyDay;
        set => SetEditorProperty(ref _monthlyDay, value);
    }

    /// <summary>Gets or sets the month in an annual rule.</summary>
    public int YearMonth
    {
        get => _yearMonth;
        set
        {
            if (!SetEditorProperty(ref _yearMonth, value))
            {
                return;
            }

            OnPropertyChanged(nameof(RecurrenceYearDayOptions));
            OnPropertyChanged(nameof(RecurrencePolicyNotice));
            OnPropertyChanged(nameof(HasRecurrencePolicyNotice));
            var maximumDay = DateTime.DaysInMonth(2000, Math.Clamp(value, 1, 12));
            if (_yearDay > maximumDay)
            {
                YearDay = maximumDay;
            }
        }
    }

    /// <summary>Gets or sets the day in an annual rule.</summary>
    public int YearDay
    {
        get => _yearDay;
        set
        {
            if (SetEditorProperty(ref _yearDay, value))
            {
                OnPropertyChanged(nameof(RecurrencePolicyNotice));
                OnPropertyChanged(nameof(HasRecurrencePolicyNotice));
            }
        }
    }

    /// <summary>Gets or sets the first effective date for a new series.</summary>
    public DateTime? RecurrenceEffectiveDateValue
    {
        get => _recurrenceEffectiveDateValue;
        set => SetEditorProperty(ref _recurrenceEffectiveDateValue, value?.Date);
    }

    /// <summary>Gets or sets the optional recurrence end date.</summary>
    public DateTime? RecurrenceEndDateValue
    {
        get => _recurrenceEndDateValue;
        set => SetEditorProperty(ref _recurrenceEndDateValue, value?.Date);
    }

    /// <summary>Gets or sets the explicit ApplyFromDate for series updates.</summary>
    public DateTime? ApplyFromDateValue
    {
        get => _applyFromDateValue;
        set => SetEditorProperty(ref _applyFromDateValue, value?.Date);
    }

    /// <summary>Gets or sets the start date for deleting unfinished future occurrences.</summary>
    public DateTime? DeleteFromDateValue
    {
        get => _deleteFromDateValue;
        set => SetEditorProperty(ref _deleteFromDateValue, value?.Date);
    }

    /// <summary>Gets or sets the time-zone identifier saved with the recurrence rule.</summary>
    public string RecurrenceTimeZoneId
    {
        get => _recurrenceTimeZoneId;
        set => SetEditorProperty(ref _recurrenceTimeZoneId, value ?? string.Empty);
    }

    /// <summary>Gets supported system time zones.</summary>
    public IReadOnlyList<RecurrenceTimeZoneOption> RecurrenceTimeZoneOptions
    {
        get => _recurrenceTimeZoneOptions;
        private set => SetProperty(ref _recurrenceTimeZoneOptions, value);
    }

    /// <summary>Gets an explicit explanation of the V1 monthly/yearly month-end behavior.</summary>
    public string RecurrencePolicyNotice => SelectedRecurrenceFrequency switch
    {
        RecurrenceFrequencyCode.Monthly => "每月 29、30、31 日在较短月份按当月最后一天处理。",
        RecurrenceFrequencyCode.Yearly when YearMonth == 2 && YearDay == 29 =>
            "每年 2 月 29 日在非闰年按 2 月最后一天处理。",
        _ => string.Empty
    };

    /// <summary>Gets whether a month-end policy explanation is currently relevant.</summary>
    public bool HasRecurrencePolicyNotice => !string.IsNullOrWhiteSpace(RecurrencePolicyNotice);

    /// <summary>Gets the recurrence-specific inline toggle or series-load message.</summary>
    public string? RecurrenceModeError
    {
        get => _recurrenceModeError;
        private set
        {
            if (SetProperty(ref _recurrenceModeError, value))
            {
                OnPropertyChanged(nameof(HasRecurrenceModeError));
            }
        }
    }

    /// <summary>Gets whether a recurrence-mode validation explanation should be shown.</summary>
    public bool HasRecurrenceModeError => !string.IsNullOrWhiteSpace(RecurrenceModeError);

    /// <summary>Gets a safe message shown when recurrence-series details cannot be loaded.</summary>
    public string? RecurrenceLoadMessage
    {
        get => _recurrenceLoadMessage;
        private set
        {
            if (SetProperty(ref _recurrenceLoadMessage, value))
            {
                OnPropertyChanged(nameof(HasRecurrenceLoadMessage));
                OnPropertyChanged(nameof(RecurrenceSeriesSummary));
            }
        }
    }

    /// <summary>Gets whether a recurrence-series load problem should be shown.</summary>
    public bool HasRecurrenceLoadMessage => !string.IsNullOrWhiteSpace(RecurrenceLoadMessage);

    /// <summary>Gets the current recurrence series summary for a materialized occurrence.</summary>
    public string RecurrenceSeriesSummary => _loadedRecurrenceSeries is null
        ? RecurrenceLoadMessage ?? "正在读取周期系列信息。"
        : FormatSeriesSummary(_loadedRecurrenceSeries);

    /// <summary>Gets the localized label for the task editor's save action.</summary>
    public string SaveButtonText => IsEditingRecurrenceSeries
        ? "保存周期系列"
        : IsRecurringCreationMode ? "创建周期任务" : "保存任务";

    /// <summary>Gets the command for clearing the optional recurrence end date.</summary>
    public IRelayCommand ClearRecurrenceEndDateCommand { get; private set; } = null!;

    /// <summary>Gets the command for switching from one occurrence to editing its series.</summary>
    public IAsyncRelayCommand EditRecurrenceSeriesCommand { get; private set; } = null!;

    /// <summary>Gets the command for returning to the current occurrence editor.</summary>
    public IAsyncRelayCommand ReturnToOccurrenceCommand { get; private set; } = null!;

    /// <summary>Gets the command for deleting only the selected occurrence.</summary>
    public IAsyncRelayCommand DeleteOccurrenceCommand { get; private set; } = null!;

    /// <summary>Gets the command for deleting unfinished occurrences from a selected date onward.</summary>
    public IAsyncRelayCommand DeleteFutureOccurrencesCommand { get; private set; } = null!;

    private bool IsRecurrenceSubmission => IsRecurringCreationMode || IsEditingRecurrenceSeries;

    private void ClearRecurrenceEndDate()
    {
        RecurrenceEndDateValue = null;
    }

    private void OnWeekdaySelectionChanged(object? sender, EventArgs eventArgs)
    {
        if (!_isApplyingValues)
        {
            MarkChanged(nameof(SelectedWeekdays));
        }
    }

    private static string FormatSeriesSummary(RecurrenceSeriesDto series)
    {
        var rule = series.Rule;
        var cadence = rule.Frequency switch
        {
            RecurrenceFrequencyCode.Daily => "每天",
            RecurrenceFrequencyCode.Weekly => $"每周 {FormatWeekdays(rule.Weekdays)}",
            RecurrenceFrequencyCode.Monthly => $"每月 {rule.MonthDay} 日",
            RecurrenceFrequencyCode.Yearly => $"每年 {rule.YearMonth} 月 {rule.YearDay} 日",
            _ => "周期规则"
        };
        var end = rule.EndDate.HasValue ? $"，结束于 {rule.EndDate:yyyy-MM-dd}" : "，无结束日期";
        return $"{cadence}，自 {rule.EffectiveDate:yyyy-MM-dd} 生效{end}。";
    }

    private static string FormatWeekdays(RecurrenceWeekdayCode weekdays)
    {
        var labels = new (RecurrenceWeekdayCode Code, string Label)[]
        {
            (RecurrenceWeekdayCode.Monday, "周一"),
            (RecurrenceWeekdayCode.Tuesday, "周二"),
            (RecurrenceWeekdayCode.Wednesday, "周三"),
            (RecurrenceWeekdayCode.Thursday, "周四"),
            (RecurrenceWeekdayCode.Friday, "周五"),
            (RecurrenceWeekdayCode.Saturday, "周六"),
            (RecurrenceWeekdayCode.Sunday, "周日")
        };
        return string.Join("、", labels.Where(item => weekdays.HasFlag(item.Code)).Select(item => item.Label));
    }

    private static DateOnly? ToDateOnly(DateTime? value) =>
        value.HasValue ? DateOnly.FromDateTime(value.Value) : null;

    private void NotifyRecurrenceStateChanged()
    {
        OnPropertyChanged(nameof(IsRecurringCreationMode));
        OnPropertyChanged(nameof(IsRecurrenceConfigurationMode));
        OnPropertyChanged(nameof(IsNotEditingRecurrenceSeries));
        OnPropertyChanged(nameof(IsTaskOnlyDataEnabled));
        OnPropertyChanged(nameof(CanManageAttachments));
        OnPropertyChanged(nameof(AttachmentsPlaceholder));
        OnPropertyChanged(nameof(IsRecurrenceEnabled));
        OnPropertyChanged(nameof(CanCreateRecurrenceSeries));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(SaveButtonText));
        NotifyRecurrenceCommandState();
        NotifyAttachmentCommandState();
    }

    private void NotifyRecurrenceTaskStateChanged()
    {
        OnPropertyChanged(nameof(IsRecurrenceOccurrence));
        OnPropertyChanged(nameof(IsOrdinaryTaskEdit));
        OnPropertyChanged(nameof(RecurrenceSeriesSummary));
        OnPropertyChanged(nameof(WindowTitle));
        NotifyRecurrenceCommandState();
    }

    private void NotifyRecurrenceInputChanged(string? propertyName)
    {
        if (propertyName is nameof(RecurrenceEffectiveDateValue)
            or nameof(RecurrenceEndDateValue)
            or nameof(ApplyFromDateValue)
            or nameof(DeleteFromDateValue)
            or nameof(RecurrenceTimeZoneId)
            or nameof(SelectedRecurrenceFrequency)
            or nameof(SelectedWeekdays)
            or nameof(MonthlyDay)
            or nameof(YearMonth)
            or nameof(YearDay)
            or nameof(IsRecurrenceEnabled))
        {
            RecurrenceModeError = null;
        }

        NotifyRecurrenceCommandState();
    }

    private void NotifyRecurrenceCommandState()
    {
        EditRecurrenceSeriesCommand?.NotifyCanExecuteChanged();
        ReturnToOccurrenceCommand?.NotifyCanExecuteChanged();
        DeleteOccurrenceCommand?.NotifyCanExecuteChanged();
        DeleteFutureOccurrencesCommand?.NotifyCanExecuteChanged();
    }
}
