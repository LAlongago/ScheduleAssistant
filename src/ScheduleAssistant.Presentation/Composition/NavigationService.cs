using CommunityToolkit.Mvvm.ComponentModel;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Default navigation state for the one-window application shell.</summary>
public sealed class NavigationService : ObservableObject, INavigationService
{
    private readonly Dictionary<NavigationPage, PageViewModelBase> _pages;
    private PageViewModelBase _currentPage;
    private NavigationPage _currentPageKey;

    /// <summary>
    /// Initializes navigation with all six page instances supplied by the composition root.
    /// </summary>
    public NavigationService(
        TodayPageViewModel today,
        WeekPageViewModel week,
        MonthPageViewModel month,
        UpcomingDeadlinesPageViewModel upcomingDeadlines,
        AllTasksPageViewModel allTasks,
        SettingsPageViewModel settings)
    {
        _pages = new Dictionary<NavigationPage, PageViewModelBase>
        {
            [NavigationPage.Today] = today,
            [NavigationPage.Week] = week,
            [NavigationPage.Month] = month,
            [NavigationPage.UpcomingDeadlines] = upcomingDeadlines,
            [NavigationPage.AllTasks] = allTasks,
            [NavigationPage.Settings] = settings
        };

        Items =
        [
            new NavigationItemViewModel(NavigationPage.Today, "今天", "⌂", "打开今天视图"),
            new NavigationItemViewModel(NavigationPage.Week, "周计划", "▦", "打开周计划视图"),
            new NavigationItemViewModel(NavigationPage.Month, "月计划", "▤", "打开月计划视图"),
            new NavigationItemViewModel(NavigationPage.UpcomingDeadlines, "即将截止", "◷", "打开即将截止视图"),
            new NavigationItemViewModel(NavigationPage.AllTasks, "全部任务", "☷", "打开全部任务视图"),
            new NavigationItemViewModel(NavigationPage.Settings, "设置", "⚙", "打开设置视图")
        ];

        _currentPageKey = NavigationPage.Today;
        _currentPage = today;
        Items[0].IsSelected = true;
    }

    /// <inheritdoc />
    public IReadOnlyList<NavigationItemViewModel> Items { get; }

    /// <inheritdoc />
    public PageViewModelBase CurrentPage => _currentPage;

    /// <inheritdoc />
    public NavigationPage CurrentPageKey => _currentPageKey;

    /// <inheritdoc />
    public void Navigate(NavigationPage page)
    {
        if (!_pages.TryGetValue(page, out var nextPage))
        {
            return;
        }

        foreach (var item in Items)
        {
            item.IsSelected = item.Page == page;
        }

        if (ReferenceEquals(_currentPage, nextPage))
        {
            return;
        }

        _currentPage = nextPage;
        _currentPageKey = page;
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(CurrentPageKey));
    }
}
