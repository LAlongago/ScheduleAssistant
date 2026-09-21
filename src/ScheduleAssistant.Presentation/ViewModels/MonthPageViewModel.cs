using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Loads and coordinates the fixed 6×7 month calendar and its in-page date details.</summary>
public sealed partial class MonthPageViewModel : PageViewModelBase, IDisposable
{
    private const int CalendarDayCount = 42;
    private readonly ITaskQueries? _queries;
    private readonly ITaskUseCases? _useCases;
    private readonly ITaskCardMapper? _cardMapper;
    private readonly IDatabaseInitialization? _databaseInitialization;
    private readonly InProcessEventBus? _eventBus;
    private readonly IUiDispatcher? _dispatcher;
    private readonly TimeProvider? _timeProvider;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly ObservableCollection<MonthCalendarDayViewModel> _days = new();
    private readonly ReadOnlyObservableCollection<MonthCalendarDayViewModel> _daysView;
    private readonly ObservableCollection<TaskCardViewModel> _dateDetails = new();
    private readonly ReadOnlyObservableCollection<TaskCardViewModel> _dateDetailsView;
    private readonly Dictionary<Guid, CategoryOptionDto> _categories = new();
    private IDisposable? _eventSubscription;
    private CancellationTokenSource? _refreshCancellation;
    private CancellationTokenSource? _dateDetailsCancellation;
    private Task _lastLoadTask = Task.CompletedTask;
    private Task _lastDateDetailsLoadTask = Task.CompletedTask;
    private DateOnly _displayedMonth;
    private DateOnly? _selectedDate;
    private bool _isDateDetailsOpen;
    private PageContentState _dateDetailsState = PageContentState.Empty;
    private string _dateDetailsStatusTitle = "选择日期";
    private string _dateDetailsStatusMessage = "点击日期查看当天的完整事项。";
    private bool _disposed;

    /// <summary>Creates an inert month page for shell-only tests.</summary>
    public MonthPageViewModel()
        : this(
            queries: null,
            useCases: null,
            cardMapper: null,
            databaseInitialization: null,
            eventBus: null,
            dispatcher: null,
            timeProvider: null,
            initialMonth: DateOnly.FromDateTime(DateTime.Today),
            initialState: PageContentState.Empty,
            statusTitle: "月计划查询未配置",
            statusMessage: "任务查询服务尚未连接。")
    {
    }

    /// <summary>Creates a real month page connected to Application ports and UI services.</summary>
    public MonthPageViewModel(
        ITaskQueries queries,
        ITaskUseCases useCases,
        ITaskCardMapper cardMapper,
        IDatabaseInitialization databaseInitialization,
        InProcessEventBus eventBus,
        IUiDispatcher dispatcher,
        TimeProvider timeProvider)
        : this(
            queries ?? throw new ArgumentNullException(nameof(queries)),
            useCases ?? throw new ArgumentNullException(nameof(useCases)),
            cardMapper ?? throw new ArgumentNullException(nameof(cardMapper)),
            databaseInitialization ?? throw new ArgumentNullException(nameof(databaseInitialization)),
            eventBus ?? throw new ArgumentNullException(nameof(eventBus)),
            dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)),
            timeProvider ?? throw new ArgumentNullException(nameof(timeProvider)),
            GetCurrentMonth(timeProvider),
            PageContentState.Loading,
            "正在加载月计划",
            "正在读取本地任务数据。")
    {
    }

    private MonthPageViewModel(
        ITaskQueries? queries,
        ITaskUseCases? useCases,
        ITaskCardMapper? cardMapper,
        IDatabaseInitialization? databaseInitialization,
        InProcessEventBus? eventBus,
        IUiDispatcher? dispatcher,
        TimeProvider? timeProvider,
        DateOnly initialMonth,
        PageContentState initialState,
        string statusTitle,
        string statusMessage)
        : base("月计划", "固定六行七列的月历与日期详情", initialState, statusTitle, statusMessage)
    {
        _queries = queries;
        _useCases = useCases;
        _cardMapper = cardMapper;
        _databaseInitialization = databaseInitialization;
        _eventBus = eventBus;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
        _displayedMonth = FirstOfMonth(initialMonth);
        _daysView = new(_days);
        _dateDetailsView = new(_dateDetails);

        PreviousMonthCommand = new AsyncRelayCommand(() => ChangeMonthAsync(-1));
        NextMonthCommand = new AsyncRelayCommand(() => ChangeMonthAsync(1));
        CurrentMonthCommand = new AsyncRelayCommand(GoToCurrentMonthAsync);
        OpenDateDetailsCommand = new AsyncRelayCommand<DateOnly>(LoadDateDetailsAsync);
        CloseDateDetailsCommand = new RelayCommand(CloseDateDetails);

        if (_eventBus is not null)
        {
            _eventSubscription = _eventBus.Subscribe<TaskApplicationEvent>(OnApplicationEventAsync);
        }
    }

    /// <summary>Gets the Monday-to-Sunday labels displayed above the grid.</summary>
    public IReadOnlyList<string> WeekdayLabels { get; } =
    [
        "周一",
        "周二",
        "周三",
        "周四",
        "周五",
        "周六",
        "周日"
    ];

    /// <summary>Gets or sets the first day of the currently displayed month.</summary>
    public DateOnly DisplayedMonth
    {
        get => _displayedMonth;
        private set
        {
            var normalized = FirstOfMonth(value);
            if (!SetProperty(ref _displayedMonth, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(DisplayedMonthText));
            OnPropertyChanged(nameof(IsCurrentMonth));
            OnPropertyChanged(nameof(GridStart));
            OnPropertyChanged(nameof(GridEnd));
        }
    }

    /// <summary>Gets the localized month heading.</summary>
    public string DisplayedMonthText => DisplayedMonth.ToString("yyyy年M月", CultureInfo.CurrentCulture);

    /// <summary>Gets whether the displayed month is the current local month.</summary>
    public bool IsCurrentMonth => _timeProvider is not null && DisplayedMonth == GetCurrentMonth(_timeProvider);

    /// <summary>Gets the first Monday represented by the current 42-day grid.</summary>
    public DateOnly GridStart => StartOfWeek(DisplayedMonth);

    /// <summary>Gets the last date represented by the current 42-day grid.</summary>
    public DateOnly GridEnd => GridStart.AddDays(CalendarDayCount - 1);

    /// <summary>Gets the fixed 42 month-grid cells.</summary>
    public ReadOnlyObservableCollection<MonthCalendarDayViewModel> Days => _daysView;

    /// <summary>Gets whether any task entry exists in the loaded grid.</summary>
    public bool HasEntries => _days.Any(day => day.HasEntries);

    /// <summary>Gets whether the in-page date details panel is open.</summary>
    public bool IsDateDetailsOpen
    {
        get => _isDateDetailsOpen;
        private set => SetProperty(ref _isDateDetailsOpen, value);
    }

    /// <summary>Gets the date currently shown in the details panel.</summary>
    public DateOnly? SelectedDate
    {
        get => _selectedDate;
        private set
        {
            if (!SetProperty(ref _selectedDate, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedDateText));
        }
    }

    /// <summary>Gets the localized details-panel date heading.</summary>
    public string SelectedDateText => SelectedDate.HasValue
        ? SelectedDate.Value.ToString("yyyy年M月d日", CultureInfo.CurrentCulture)
        : string.Empty;

    /// <summary>Gets the state of the selected-date details query.</summary>
    public PageContentState DateDetailsState
    {
        get => _dateDetailsState;
        private set => SetProperty(ref _dateDetailsState, value);
    }

    /// <summary>Gets the selected-date details status title.</summary>
    public string DateDetailsStatusTitle
    {
        get => _dateDetailsStatusTitle;
        private set => SetProperty(ref _dateDetailsStatusTitle, value);
    }

    /// <summary>Gets the selected-date details status message.</summary>
    public string DateDetailsStatusMessage
    {
        get => _dateDetailsStatusMessage;
        private set => SetProperty(ref _dateDetailsStatusMessage, value);
    }

    /// <summary>Gets all tasks returned for the selected date.</summary>
    public ReadOnlyObservableCollection<TaskCardViewModel> DateDetails => _dateDetailsView;

    /// <summary>Gets the previous-month navigation command inside the month page.</summary>
    public IAsyncRelayCommand PreviousMonthCommand { get; }

    /// <summary>Gets the next-month navigation command inside the month page.</summary>
    public IAsyncRelayCommand NextMonthCommand { get; }

    /// <summary>Gets the return-to-current-month command.</summary>
    public IAsyncRelayCommand CurrentMonthCommand { get; }

    /// <summary>Gets the date-details command used by date headers and “+N”.</summary>
    public IAsyncRelayCommand<DateOnly> OpenDateDetailsCommand { get; }

    /// <summary>Gets the command that closes the in-page date details panel.</summary>
    public IRelayCommand CloseDateDetailsCommand { get; }

    /// <summary>Gets the most recent month load operation for deterministic tests.</summary>
    public Task LastLoadTask => _lastLoadTask;

    /// <summary>Gets the most recent date-details load operation for deterministic tests.</summary>
    public Task LastDateDetailsLoadTask => _lastDateDetailsLoadTask;

    /// <summary>Loads the current 42-day grid after database initialization.</summary>
    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _lastLoadTask = _queries is null ? SetUnconfiguredStateAsync() : RefreshAsync(cancellationToken);
        return _lastLoadTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _eventSubscription?.Dispose();
        _eventSubscription = null;
        CancelAndDispose(ref _refreshCancellation);
        CancelAndDispose(ref _dateDetailsCancellation);
        _refreshGate.Dispose();
    }

    private async Task ChangeMonthAsync(int monthOffset)
    {
        var targetMonth = DisplayedMonth.AddMonths(monthOffset);
        await InvokeOnUiAsync(() =>
        {
            DisplayedMonth = targetMonth;
            CloseDateDetails();
        }).ConfigureAwait(false);
        await LoadAsync().ConfigureAwait(false);
    }

    private async Task GoToCurrentMonthAsync()
    {
        var currentMonth = _timeProvider is null
            ? DisplayedMonth
            : GetCurrentMonth(_timeProvider);
        await InvokeOnUiAsync(() =>
        {
            DisplayedMonth = currentMonth;
            CloseDateDetails();
        }).ConfigureAwait(false);
        await LoadAsync().ConfigureAwait(false);
    }

    private static DateOnly GetCurrentMonth(TimeProvider timeProvider)
    {
        return FirstOfMonth(GetTodayLocal(timeProvider));
    }

    private static DateOnly GetTodayLocal(TimeProvider timeProvider)
    {
        return DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
    }

    private static DateOnly FirstOfMonth(DateOnly date)
    {
        return new DateOnly(date.Year, date.Month, 1);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysFromMonday);
    }

    private static void CancelAndDispose(ref CancellationTokenSource? cancellation)
    {
        var source = Interlocked.Exchange(ref cancellation, null);
        if (source is null)
        {
            return;
        }

        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The source was already completed by its operation.
        }

        source.Dispose();
    }
}
