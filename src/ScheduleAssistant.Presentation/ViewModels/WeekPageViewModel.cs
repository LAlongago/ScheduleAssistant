using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Loads and navigates the fixed Monday-to-Sunday week calendar.</summary>
public sealed class WeekPageViewModel : PageViewModelBase, IDisposable
{
    private static readonly DateOnly InertWeekStart = new(2000, 1, 3);

    private readonly ITaskQueries? _queries;
    private readonly ITaskUseCases? _useCases;
    private readonly ITaskCardMapper? _cardMapper;
    private readonly IDatabaseInitialization? _databaseInitialization;
    private readonly InProcessEventBus? _eventBus;
    private readonly IUiDispatcher? _dispatcher;
    private readonly TimeProvider? _timeProvider;
    private readonly ObservableCollection<WeekDayViewModel> _weekDays = new();
    private readonly ReadOnlyObservableCollection<WeekDayViewModel> _weekDaysView;
    private readonly Dictionary<Guid, CategoryOptionDto> _categories = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IDisposable? _eventSubscription;
    private CancellationTokenSource? _refreshCancellation;
    private Task _lastLoadTask = Task.CompletedTask;
    private DateOnly _weekStart;
    private bool _disposed;

    /// <summary>Creates an inert instance for shell tests that do not load data.</summary>
    public WeekPageViewModel()
        : base("周计划", "周一至周日的七列计划视图", PageContentState.Loading, "正在加载周计划", "正在读取本地任务数据。")
    {
        _weekDaysView = new(_weekDays);
        _weekStart = InertWeekStart;
        PreviousWeekCommand = new AsyncRelayCommand(GoToPreviousWeekAsync);
        NextWeekCommand = new AsyncRelayCommand(GoToNextWeekAsync);
        CurrentWeekCommand = new AsyncRelayCommand(GoToCurrentWeekAsync);
    }

    /// <summary>Creates a real week page connected to Application ports and local services.</summary>
    public WeekPageViewModel(
        ITaskQueries queries,
        ITaskUseCases useCases,
        ITaskCardMapper cardMapper,
        IDatabaseInitialization databaseInitialization,
        InProcessEventBus eventBus,
        IUiDispatcher dispatcher,
        TimeProvider timeProvider)
        : this()
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _useCases = useCases ?? throw new ArgumentNullException(nameof(useCases));
        _cardMapper = cardMapper ?? throw new ArgumentNullException(nameof(cardMapper));
        _databaseInitialization = databaseInitialization ?? throw new ArgumentNullException(nameof(databaseInitialization));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _weekStart = GetWeekStart(GetTodayLocal(timeProvider));
        _eventSubscription = _eventBus.Subscribe<TaskApplicationEvent>(OnApplicationEventAsync);
    }

    /// <summary>Gets the Monday date for the currently displayed week.</summary>
    public DateOnly WeekStart
    {
        get => _weekStart;
        private set
        {
            if (!SetProperty(ref _weekStart, value))
            {
                return;
            }

            OnPropertyChanged(nameof(WeekEnd));
            OnPropertyChanged(nameof(WeekRangeText));
        }
    }

    /// <summary>Gets the Sunday date for the currently displayed week.</summary>
    public DateOnly WeekEnd => WeekStart.AddDays(6);

    /// <summary>Gets the localized date range shown by the week navigation.</summary>
    public string WeekRangeText => $"{FormatDate(WeekStart)} – {FormatDate(WeekEnd)}";

    /// <summary>Gets the seven Application-provided days in Monday-to-Sunday order.</summary>
    public ReadOnlyObservableCollection<WeekDayViewModel> WeekDays => _weekDaysView;

    /// <summary>Gets whether at least one task is available in the displayed week.</summary>
    public bool HasTasks => _weekDays.Any(day => day.HasTasks);

    /// <summary>Gets the command that loads the previous week.</summary>
    public IAsyncRelayCommand PreviousWeekCommand { get; }

    /// <summary>Gets the command that loads the next week.</summary>
    public IAsyncRelayCommand NextWeekCommand { get; }

    /// <summary>Gets the command that returns to the week containing the local current date.</summary>
    public IAsyncRelayCommand CurrentWeekCommand { get; }

    /// <summary>Gets the last load task, useful for deterministic page tests.</summary>
    public Task LastLoadTask => _lastLoadTask;

    /// <summary>Loads the selected week asynchronously after database initialization.</summary>
    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var requestedWeekStart = WeekStart;
        _lastLoadTask = _queries is null
            ? SetUnconfiguredStateAsync()
            : RefreshAsync(requestedWeekStart, cancellationToken);
        return _lastLoadTask;
    }

    /// <summary>Moves the page to the previous Monday-to-Sunday week.</summary>
    public Task GoToPreviousWeekAsync()
    {
        return NavigateToWeekAsync(WeekStart.AddDays(-7));
    }

    /// <summary>Moves the page to the next Monday-to-Sunday week.</summary>
    public Task GoToNextWeekAsync()
    {
        return NavigateToWeekAsync(WeekStart.AddDays(7));
    }

    /// <summary>Moves the page to the week containing the local current date.</summary>
    public Task GoToCurrentWeekAsync()
    {
        if (_timeProvider is null)
        {
            return SetUnconfiguredStateAsync();
        }

        return NavigateToWeekAsync(GetWeekStart(GetTodayLocal(_timeProvider)));
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
        var refreshCancellation = Interlocked.Exchange(ref _refreshCancellation, null);
        try
        {
            refreshCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A completed refresh may have disposed its source just before page disposal.
        }

        _refreshGate.Dispose();
    }

    private Task NavigateToWeekAsync(DateOnly date)
    {
        WeekStart = GetWeekStart(date);
        return LoadAsync();
    }

    private async Task RefreshAsync(DateOnly requestedWeekStart, CancellationToken cancellationToken)
    {
        if (_queries is null || _useCases is null || _cardMapper is null || _databaseInitialization is null
            || _dispatcher is null || _disposed)
        {
            await SetUnconfiguredStateAsync().ConfigureAwait(false);
            return;
        }

        using var refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previousCancellation = Interlocked.Exchange(ref _refreshCancellation, refreshCancellation);
        try
        {
            previousCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A previous completed refresh may already have released its token source.
        }

        try
        {
            await _refreshGate.WaitAsync(refreshCancellation.Token).ConfigureAwait(false);
            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    SetContentState(PageContentState.Loading, "正在加载周计划", "正在读取本周任务数据。");
                }).ConfigureAwait(false);

                await _databaseInitialization.EnsureInitializedAsync(refreshCancellation.Token).ConfigureAwait(false);
                var calendarTask = _queries.GetWeekCalendarAsync(
                    new GetWeekCalendarQuery(requestedWeekStart),
                    refreshCancellation.Token);
                var categoriesTask = _categories.Count == 0
                    ? _useCases.GetCategoryOptionsAsync(new GetCategoryOptionsQuery(), refreshCancellation.Token)
                    : Task.FromResult(ApplicationResult<IReadOnlyList<CategoryOptionDto>>.Success(_categories.Values.ToArray()));

                await Task.WhenAll(calendarTask, categoriesTask).ConfigureAwait(false);
                refreshCancellation.Token.ThrowIfCancellationRequested();
                var calendar = await calendarTask.ConfigureAwait(false);
                var categories = await categoriesTask.ConfigureAwait(false);
                var error = FirstError(calendar.Error, categories.Error);
                if (error is not null || calendar.Value is null || categories.Value is null)
                {
                    await SetErrorAsync(
                        error?.Message ?? "周计划数据暂时无法读取，请稍后重试。",
                        requestedWeekStart,
                        refreshCancellation.Token).ConfigureAwait(false);
                    return;
                }

                await _dispatcher.InvokeAsync(() =>
                {
                    if (!_disposed && WeekStart == requestedWeekStart)
                    {
                        ApplyResults(calendar.Value, categories.Value);
                    }
                }).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    _refreshGate.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Disposal may race a cancelled refresh during application shutdown.
                }
            }
        }
        catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested || _disposed)
        {
            // A newer navigation/event refresh or page disposal superseded this query.
        }
        catch
        {
            await SetErrorAsync(
                "周计划数据暂时无法读取，请稍后重试。",
                requestedWeekStart,
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.CompareExchange(ref _refreshCancellation, null, refreshCancellation);
            refreshCancellation.Dispose();
        }
    }

    private void ApplyResults(
        WeekCalendarDto calendar,
        IReadOnlyList<CategoryOptionDto> categories)
    {
        if (calendar.Days.Count != 7)
        {
            SetContentState(PageContentState.Error, "周计划加载失败", "周历数据格式不完整，请稍后重试。");
            return;
        }

        _categories.Clear();
        foreach (var category in categories)
        {
            _categories[category.Id] = category;
        }

        _weekDays.Clear();
        var today = GetTodayLocal(_timeProvider!);
        foreach (var day in calendar.Days)
        {
            var cards = day.Entries
                .Select(entry => _cardMapper!.Map(entry, _categories, TaskCardMode.Compact))
                .ToArray();
            _weekDays.Add(new WeekDayViewModel(day.Date, cards, day.Date == today));
        }

        OnPropertyChanged(nameof(HasTasks));
        var hasTasks = HasTasks;
        SetContentState(
            hasTasks ? PageContentState.Ready : PageContentState.Empty,
            hasTasks ? "周计划" : "本周没有安排",
            hasTasks ? "周一至周日的任务已加载。" : "本周没有计划或 Deadline。");
    }

    private Task OnApplicationEventAsync(
        TaskApplicationEvent applicationEvent,
        CancellationToken cancellationToken)
    {
        if (_disposed
            || applicationEvent is ReminderPlanChanged
            || !applicationEvent.AffectedDates.Any(IsInCurrentWeek))
        {
            return Task.CompletedTask;
        }

        return LoadAsync(cancellationToken);
    }

    private async Task SetErrorAsync(
        string message,
        DateOnly requestedWeekStart,
        CancellationToken cancellationToken)
    {
        if (_dispatcher is null || _disposed)
        {
            return;
        }

        await _dispatcher.InvokeAsync(() =>
        {
            if (!_disposed && WeekStart == requestedWeekStart && !cancellationToken.IsCancellationRequested)
            {
                SetContentState(PageContentState.Error, "周计划加载失败", message);
            }
        }).ConfigureAwait(false);
    }

    private Task SetUnconfiguredStateAsync()
    {
        SetContentState(PageContentState.Error, "周计划查询未配置", "任务查询服务尚未连接。");
        return Task.CompletedTask;
    }

    private bool IsInCurrentWeek(DateOnly date)
    {
        return date >= WeekStart && date <= WeekEnd;
    }

    private static DateOnly GetTodayLocal(TimeProvider timeProvider)
    {
        var localNow = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow().ToUniversalTime(),
            timeProvider.LocalTimeZone);
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysFromMonday);
    }

    private static string FormatDate(DateOnly date) => $"{date.Year}年{date.Month}月{date.Day}日";

    private static ApplicationError? FirstError(params ApplicationError?[] errors)
    {
        return errors.FirstOrDefault(error => error is not null);
    }
}
