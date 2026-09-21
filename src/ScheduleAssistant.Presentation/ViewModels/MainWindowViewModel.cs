using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScheduleAssistant.Presentation.Composition;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>
/// Coordinates shell state and commands. It does not call repositories, SQLite, files, or Windows APIs.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly ITaskEditorService? _taskEditorService;
    private readonly bool _isDateNavigationEnabled;
    private readonly bool _isSearchEnabled;
    private readonly bool _isCreateTaskEnabled;
    private readonly string _dateNavigationHint;
    private readonly string _searchHint;
    private readonly string _createTaskHint;
    private string _shellNotice;
    private string _searchText = string.Empty;

    /// <summary>
    /// Initializes the shell ViewModel with navigation, clock, and the reusable task-editor entry point.
    /// </summary>
    public MainWindowViewModel(
        INavigationService navigationService,
        TimeProvider timeProvider,
        ITaskEditorService? taskEditorService = null)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _navigationService = navigationService;
        _taskEditorService = taskEditorService;
        _isDateNavigationEnabled = false;
        _isSearchEnabled = false;
        _isCreateTaskEnabled = taskEditorService is not null;
        _dateNavigationHint = "周、月日历和日期导航将在后续页面任务中开放";
        _searchHint = "全部任务搜索将在后续查询页面中开放";
        _createTaskHint = taskEditorService is null
            ? "当前组合未提供任务编辑器服务"
            : "打开普通任务编辑器";
        _shellNotice = taskEditorService is null
            ? "当前组合未连接任务编辑器"
            : "今天、即将截止和任务编辑器已连接；周、月、全部任务、搜索与系统集成功能按后续任务开放";
        DisplayDate = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        NavigateCommand = new RelayCommand<NavigationPage>(Navigate);
        PreviousDateCommand = new RelayCommand(DisabledDateAction, () => false);
        CurrentDateCommand = new RelayCommand(DisabledDateAction, () => false);
        NextDateCommand = new RelayCommand(DisabledDateAction, () => false);
        CreateTaskCommand = taskEditorService is null
            ? new AsyncRelayCommand(DisabledCreateActionAsync, () => false)
            : new AsyncRelayCommand(CreateTaskAsync);

        _navigationService.PropertyChanged += OnNavigationPropertyChanged;
    }

    /// <summary>Gets the navigation items displayed in the left rail.</summary>
    public IReadOnlyList<NavigationItemViewModel> NavigationItems => _navigationService.Items;

    /// <summary>Gets the active page ViewModel displayed in the content region.</summary>
    public PageViewModelBase CurrentPage => _navigationService.CurrentPage;

    /// <summary>Gets the active page title displayed in the header and window chrome.</summary>
    public string CurrentTitle => CurrentPage.Title;

    /// <summary>Gets the active page description displayed below the title.</summary>
    public string CurrentSubtitle => CurrentPage.Description;

    /// <summary>Gets the stable shell date label used while date queries are not connected.</summary>
    public DateOnly DisplayDate { get; }

    /// <summary>Gets the localized display form of the shell date.</summary>
    public string DisplayDateText => DisplayDate.ToString("yyyy年M月d日", CultureInfo.CurrentCulture);

    /// <summary>Gets or sets the future search input binding.</summary>
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    /// <summary>Gets whether the date controls have an implemented command.</summary>
    public bool IsDateNavigationEnabled => _isDateNavigationEnabled;

    /// <summary>Gets whether the search control has an implemented query.</summary>
    public bool IsSearchEnabled => _isSearchEnabled;

    /// <summary>Gets whether the new-task command has an implemented editor.</summary>
    public bool IsCreateTaskEnabled => _isCreateTaskEnabled;

    /// <summary>Explains why date navigation is disabled.</summary>
    public string DateNavigationHint => _dateNavigationHint;

    /// <summary>Explains why search is disabled.</summary>
    public string SearchHint => _searchHint;

    /// <summary>Explains why creating a task is disabled.</summary>
    public string CreateTaskHint => _createTaskHint;

    /// <summary>Gets the persistent shell notice shown in the footer.</summary>
    public string ShellNotice => _shellNotice;

    /// <summary>Gets the navigation command bound by the left rail.</summary>
    public IRelayCommand<NavigationPage> NavigateCommand { get; }

    /// <summary>Gets the reserved previous-date command placeholder.</summary>
    public IRelayCommand PreviousDateCommand { get; }

    /// <summary>Gets the reserved current-date command placeholder.</summary>
    public IRelayCommand CurrentDateCommand { get; }

    /// <summary>Gets the reserved next-date command placeholder.</summary>
    public IRelayCommand NextDateCommand { get; }

    /// <summary>Gets the new-task command bound to the reusable task-editor service.</summary>
    public IAsyncRelayCommand CreateTaskCommand { get; }

    private void Navigate(NavigationPage page) => _navigationService.Navigate(page);

    private static void DisabledDateAction()
    {
        // Deliberately empty: CanExecute is false until a date query is connected.
    }

    private static Task DisabledCreateActionAsync()
    {
        return Task.CompletedTask;
    }

    private async Task CreateTaskAsync()
    {
        if (_taskEditorService is null)
        {
            return;
        }

        try
        {
            await _taskEditorService.OpenCreateAsync(DisplayDate).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            _shellNotice = "新建任务操作已取消";
            OnPropertyChanged(nameof(ShellNotice));
        }
        catch
        {
            _shellNotice = "无法打开任务编辑器，请重试";
            OnPropertyChanged(nameof(ShellNotice));
        }
    }

    private void OnNavigationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName == nameof(INavigationService.CurrentPage)
            || e.PropertyName == nameof(INavigationService.CurrentPageKey))
        {
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(CurrentTitle));
            OnPropertyChanged(nameof(CurrentSubtitle));
        }
    }
}
