using System.Collections.ObjectModel;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Presentation-only header and ordered cards for one Application calendar day.</summary>
public sealed class WeekDayViewModel
{
    private static readonly Dictionary<DayOfWeek, string> WeekdayNames =
        new Dictionary<DayOfWeek, string>
        {
            [DayOfWeek.Monday] = "周一",
            [DayOfWeek.Tuesday] = "周二",
            [DayOfWeek.Wednesday] = "周三",
            [DayOfWeek.Thursday] = "周四",
            [DayOfWeek.Friday] = "周五",
            [DayOfWeek.Saturday] = "周六",
            [DayOfWeek.Sunday] = "周日"
        };

    /// <summary>Initializes one displayed day without re-aggregating its entries.</summary>
    public WeekDayViewModel(
        DateOnly date,
        IReadOnlyList<TaskCardViewModel> tasks,
        bool isToday)
    {
        Date = date;
        Tasks = new ReadOnlyObservableCollection<TaskCardViewModel>(
            new ObservableCollection<TaskCardViewModel>(tasks));
        IsToday = isToday;
    }

    /// <summary>Gets the local date represented by this column.</summary>
    public DateOnly Date { get; }

    /// <summary>Gets the localized Monday-to-Sunday weekday label.</summary>
    public string WeekdayText => WeekdayNames[Date.DayOfWeek];

    /// <summary>Gets the compact month/day header label.</summary>
    public string DateText => $"{Date.Month}月{Date.Day}日";

    /// <summary>Gets whether this column is the current local date.</summary>
    public bool IsToday { get; }

    /// <summary>Gets entries in the exact order supplied by the Application query.</summary>
    public ReadOnlyObservableCollection<TaskCardViewModel> Tasks { get; }

    /// <summary>Gets whether the day contains any plan or Deadline entries.</summary>
    public bool HasTasks => Tasks.Count > 0;
}
