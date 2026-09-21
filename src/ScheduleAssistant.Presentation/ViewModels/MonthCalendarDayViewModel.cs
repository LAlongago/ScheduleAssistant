using System.Globalization;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Tasks;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Presentation projection for one of the 42 cells in the month grid.</summary>
public sealed class MonthCalendarDayViewModel
{
    /// <summary>The maximum number of compact entries shown inside a month cell.</summary>
    public const int VisibleEntryLimit = 3;

    /// <summary>Initializes a month-grid cell from the Application calendar result.</summary>
    public MonthCalendarDayViewModel(
        CalendarDayDto day,
        bool isToday,
        ITaskCardMapper cardMapper,
        IReadOnlyDictionary<Guid, CategoryOptionDto> categories)
    {
        ArgumentNullException.ThrowIfNull(day);
        ArgumentNullException.ThrowIfNull(cardMapper);
        ArgumentNullException.ThrowIfNull(categories);

        Date = day.Date;
        IsInDisplayedMonth = day.IsInDisplayedMonth;
        IsToday = isToday;
        Entries = day.Entries;
        DayNumberText = day.Date.Day.ToString(CultureInfo.CurrentCulture);
        AccessibleDateText = day.Date.ToString("yyyy年M月d日", CultureInfo.CurrentCulture);
        VisibleEntries = day.Entries
            .Take(VisibleEntryLimit)
            .Select(entry => cardMapper.Map(entry, categories, TaskCardMode.Compact))
            .ToArray();
        AdditionalEntryCount = Math.Max(0, day.Entries.Count - VisibleEntries.Count);
    }

    /// <summary>Gets the local date represented by this cell.</summary>
    public DateOnly Date { get; }

    /// <summary>Gets the localized day number shown in the cell header.</summary>
    public string DayNumberText { get; }

    /// <summary>Gets an accessible full-date label for the cell header.</summary>
    public string AccessibleDateText { get; }

    /// <summary>Gets whether this cell belongs to the requested month.</summary>
    public bool IsInDisplayedMonth { get; }

    /// <summary>Gets whether this cell is the current local date.</summary>
    public bool IsToday { get; }

    /// <summary>Gets all entries returned for this date by Application.</summary>
    public IReadOnlyList<CalendarEntry> Entries { get; }

    /// <summary>Gets at most three compact cards selected in Application-provided order.</summary>
    public IReadOnlyList<TaskCardViewModel> VisibleEntries { get; }

    /// <summary>Gets the number of entries omitted from the compact cell.</summary>
    public int AdditionalEntryCount { get; }

    /// <summary>Gets whether this cell has at least one calendar entry.</summary>
    public bool HasEntries => Entries.Count > 0;

    /// <summary>Gets whether the cell needs a “+N” details affordance.</summary>
    public bool HasAdditionalEntries => AdditionalEntryCount > 0;
}
