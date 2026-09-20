using System.Collections.ObjectModel;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Real today-page query, grouping, countdown, and local-refresh state.</summary>
public sealed class TodayPageViewModel : PageViewModelBase, IDisposable
{
    private readonly ITaskQueries? _queries;
    private readonly ITaskUseCases? _useCases;
    private readonly ITaskCardMapper? _cardMapper;
    private readonly IDatabaseInitialization? _databaseInitialization;
    private readonly InProcessEventBus? _eventBus;
    private readonly IUiDispatcher? _dispatcher;
    private readonly TimeProvider? _timeProvider;
    private readonly IDeadlineRefreshTimer? _timer;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Dictionary<Guid, CategoryOptionDto> _categories = new();
    private readonly ObservableCollection<DeadlineCountdownViewModel> _additionalDeadlines = new();
    private readonly ReadOnlyObservableCollection<DeadlineCountdownViewModel> _additionalDeadlinesView;
    private IDisposable? _eventSubscription;
    private CancellationTokenSource? _refreshCancellation;
    private Task _lastLoadTask = Task.CompletedTask;
    private DeadlineCountdownViewModel? _recentDeadline;
    private bool _isCompletedExpanded;
    private bool _disposed;

    /// <summary>Creates an inert instance for shell unit tests that do not load data.</summary>
    public TodayPageViewModel()
        : base("今天", "计划与 Deadline 的单日概览", PageContentState.Loading, "正在加载今天", "正在读取本地任务数据。")
    {
        _additionalDeadlinesView = new(_additionalDeadlines);
    }

    /// <summary>Creates a real today page connected to Application ports and local services.</summary>
    public TodayPageViewModel(
        ITaskQueries queries,
        ITaskUseCases useCases,
        ITaskCardMapper cardMapper,
        IDatabaseInitialization databaseInitialization,
        InProcessEventBus eventBus,
        IUiDispatcher dispatcher,
        TimeProvider timeProvider,
        IDeadlineRefreshTimer timer)
        : base("今天", "计划与 Deadline 的单日概览", PageContentState.Loading, "正在加载今天", "正在读取本地任务数据。")
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _useCases = useCases ?? throw new ArgumentNullException(nameof(useCases));
        _cardMapper = cardMapper ?? throw new ArgumentNullException(nameof(cardMapper));
        _databaseInitialization = databaseInitialization ?? throw new ArgumentNullException(nameof(databaseInitialization));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _timer = timer ?? throw new ArgumentNullException(nameof(timer));
        _additionalDeadlinesView = new(_additionalDeadlines);
        _eventSubscription = _eventBus.Subscribe<TaskApplicationEvent>(OnApplicationEventAsync);
        _timer.Start(OnMinuteTickAsync, OnDeadlineReachedAsync);
    }

    /// <summary>Gets the tasks whose Deadline is already overdue.</summary>
    public ObservableCollection<TaskCardViewModel> OverdueTasks { get; } = new();

    /// <summary>Gets incomplete tasks whose original plan date has passed.</summary>
    public ObservableCollection<TaskCardViewModel> PlannedPastTasks { get; } = new();

    /// <summary>Gets tasks planned for today, including combined plan/Deadline cards.</summary>
    public ObservableCollection<TaskCardViewModel> PlannedTodayTasks { get; } = new();

    /// <summary>Gets Deadline-only tasks due today.</summary>
    public ObservableCollection<TaskCardViewModel> DeadlineTodayTasks { get; } = new();

    /// <summary>Gets completed tasks associated with today's local date.</summary>
    public ObservableCollection<TaskCardViewModel> CompletedTasks { get; } = new();

    /// <summary>Gets the top cached incomplete Deadline.</summary>
    public DeadlineCountdownViewModel? RecentDeadline
    {
        get => _recentDeadline;
        private set => SetProperty(ref _recentDeadline, value);
    }

    /// <summary>Gets up to three additional cached Deadline summaries.</summary>
    public ReadOnlyObservableCollection<DeadlineCountdownViewModel> AdditionalDeadlines => _additionalDeadlinesView;

    /// <summary>Gets or sets whether the completed section is expanded.</summary>
    public bool IsCompletedExpanded
    {
        get => _isCompletedExpanded;
        set => SetProperty(ref _isCompletedExpanded, value);
    }

    /// <summary>Gets whether any countdown summary is available.</summary>
    public bool HasCountdown => RecentDeadline is not null;

    /// <summary>Gets whether the lightweight no-Deadline hint should be shown.</summary>
    public bool HasNoDeadlineCountdown => !HasCountdown;

    /// <summary>Gets whether the overdue section has cards.</summary>
    public bool HasOverdueTasks => OverdueTasks.Count > 0;

    /// <summary>Gets whether the planned-past section has cards.</summary>
    public bool HasPlannedPastTasks => PlannedPastTasks.Count > 0;

    /// <summary>Gets whether today's plan section has cards.</summary>
    public bool HasPlannedTodayTasks => PlannedTodayTasks.Count > 0;

    /// <summary>Gets whether today's Deadline-only section has cards.</summary>
    public bool HasDeadlineTodayTasks => DeadlineTodayTasks.Count > 0;

    /// <summary>Gets whether the completed section has cards.</summary>
    public bool HasCompletedTasks => CompletedTasks.Count > 0;

    /// <summary>Gets whether the page has any normal content to render.</summary>
    public bool HasNormalContent => HasCountdown || HasOverdueTasks || HasPlannedPastTasks || HasPlannedTodayTasks || HasDeadlineTodayTasks || HasCompletedTasks;

    /// <summary>Gets the last load task, useful for deterministic page tests.</summary>
    public Task LastLoadTask => _lastLoadTask;

    /// <summary>Loads today data asynchronously after the one-shot database initialization.</summary>
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
        var refreshCancellation = Interlocked.Exchange(ref _refreshCancellation, null);
        try
        {
            refreshCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A completed refresh may have disposed its source just before page disposal.
        }
        _timer?.Dispose();
        _refreshGate.Dispose();
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_queries is null || _useCases is null || _cardMapper is null || _databaseInitialization is null
            || _dispatcher is null || _timeProvider is null || _timer is null || _disposed)
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
                    SetContentState(PageContentState.Loading, "正在加载今天", "正在读取本地任务数据。");
                }).ConfigureAwait(false);

                await _databaseInitialization.EnsureInitializedAsync(refreshCancellation.Token).ConfigureAwait(false);
                var today = GetTodayLocal(_timeProvider);
                var pendingTask = _queries.GetTodayPendingAsync(new GetTodayPendingQuery(), refreshCancellation.Token);
                var calendarTask = _queries.GetCalendarDateAsync(new GetCalendarDateQuery(today), refreshCancellation.Token);
                var deadlinesTask = _queries.GetDeadlinesAsync(new GetDeadlinesQuery(DeadlineQueryRange.All), refreshCancellation.Token);
                var categoriesTask = _categories.Count == 0
                    ? _useCases.GetCategoryOptionsAsync(new GetCategoryOptionsQuery(), refreshCancellation.Token)
                    : Task.FromResult(ApplicationResult<IReadOnlyList<CategoryOptionDto>>.Success(_categories.Values.ToArray()));

                await Task.WhenAll(pendingTask, calendarTask, deadlinesTask, categoriesTask).ConfigureAwait(false);
                refreshCancellation.Token.ThrowIfCancellationRequested();

                var pending = await pendingTask.ConfigureAwait(false);
                var calendar = await calendarTask.ConfigureAwait(false);
                var deadlines = await deadlinesTask.ConfigureAwait(false);
                var categories = await categoriesTask.ConfigureAwait(false);
                var error = FirstError(pending.Error, calendar.Error, deadlines.Error, categories.Error);
                if (error is not null || pending.Value is null || calendar.Value is null
                    || deadlines.Value is null || categories.Value is null)
                {
                    await SetErrorAsync(error?.Message ?? "今天数据暂时无法读取，请稍后重试。", refreshCancellation.Token).ConfigureAwait(false);
                    return;
                }

                await _dispatcher.InvokeAsync(() => ApplyResults(
                    pending.Value,
                    calendar.Value,
                    deadlines.Value,
                    categories.Value,
                    _timeProvider.GetUtcNow().ToUniversalTime())).ConfigureAwait(false);
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
            // A newer local refresh or page disposal superseded this query.
        }
        catch
        {
            await SetErrorAsync("今天数据暂时无法读取，请稍后重试。", CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.CompareExchange(ref _refreshCancellation, null, refreshCancellation);
            refreshCancellation.Dispose();
        }
    }

    private void ApplyResults(
        TodayPendingDto pending,
        CalendarDayDto calendar,
        DeadlineQueryResult deadlines,
        IReadOnlyList<CategoryOptionDto> categories,
        DateTimeOffset nowUtc)
    {
        _categories.Clear();
        foreach (var category in categories)
        {
            _categories[category.Id] = category;
        }

        OverdueTasks.Clear();
        PlannedPastTasks.Clear();
        PlannedTodayTasks.Clear();
        DeadlineTodayTasks.Clear();
        CompletedTasks.Clear();

        foreach (var entry in pending.Entries)
        {
            var target = entry.DisplayStatus switch
            {
                DisplayStatusCode.Overdue => OverdueTasks,
                DisplayStatusCode.PlannedPast => PlannedPastTasks,
                _ when entry.IsPlannedOnDate => PlannedTodayTasks,
                _ when entry.IsDeadlineOnDate => DeadlineTodayTasks,
                _ => PlannedPastTasks
            };
            target.Add(_cardMapper!.Map(entry, _categories));
        }

        foreach (var entry in calendar.Entries.Where(entry => entry.DisplayStatus == DisplayStatusCode.Completed))
        {
            CompletedTasks.Add(_cardMapper!.Map(entry, _categories));
        }

        var orderedDeadlines = deadlines.Overdue.Concat(deadlines.Upcoming).Take(4).ToArray();
        var countdowns = orderedDeadlines
            .Select(entry => new DeadlineCountdownViewModel(
                entry,
                _categories.GetValueOrDefault(entry.Task.CategoryId),
                nowUtc))
            .ToArray();
        RecentDeadline = countdowns.FirstOrDefault();
        _additionalDeadlines.Clear();
        foreach (var countdown in countdowns.Skip(1))
        {
            _additionalDeadlines.Add(countdown);
        }

        OnPropertyChanged(nameof(HasCountdown));
        OnPropertyChanged(nameof(HasNoDeadlineCountdown));
        OnPropertyChanged(nameof(HasOverdueTasks));
        OnPropertyChanged(nameof(HasPlannedPastTasks));
        OnPropertyChanged(nameof(HasPlannedTodayTasks));
        OnPropertyChanged(nameof(HasDeadlineTodayTasks));
        OnPropertyChanged(nameof(HasCompletedTasks));
        OnPropertyChanged(nameof(HasNormalContent));

        var nextDeadline = deadlines.Upcoming
            .Select(entry => entry.Task.Deadline?.Utc)
            .Where(deadline => deadline.HasValue && deadline.Value > nowUtc)
            .Select(deadline => deadline!.Value)
            .OrderBy(deadline => deadline)
            .FirstOrDefault();
        _timer!.ScheduleDeadline(nextDeadline == default ? null : nextDeadline);

        var hasContent = HasNormalContent;
        SetContentState(
            hasContent ? PageContentState.Ready : PageContentState.Empty,
            hasContent ? "今天" : "今天没有安排",
            hasContent ? "今天数据已加载。" : "没有未完成的计划或 Deadline。已完成任务也会在有记录时显示。");
    }

    private async Task OnMinuteTickAsync()
    {
        if (_dispatcher is null || _timeProvider is null || _disposed)
        {
            return;
        }

        await _dispatcher.InvokeAsync(() =>
        {
            var nowUtc = _timeProvider.GetUtcNow().ToUniversalTime();
            RecentDeadline?.Update(nowUtc);
            foreach (var deadline in _additionalDeadlines)
            {
                deadline.Update(nowUtc);
            }
        }).ConfigureAwait(false);
    }

    private Task OnDeadlineReachedAsync()
    {
        return _disposed ? Task.CompletedTask : LoadAsync();
    }

    private Task OnApplicationEventAsync(TaskApplicationEvent applicationEvent, CancellationToken cancellationToken)
    {
        if (_disposed || applicationEvent is ReminderPlanChanged)
        {
            return Task.CompletedTask;
        }

        return LoadAsync(cancellationToken);
    }

    private async Task SetErrorAsync(string message, CancellationToken cancellationToken)
    {
        if (_dispatcher is null || _disposed)
        {
            return;
        }

        await _dispatcher.InvokeAsync(() =>
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                SetContentState(PageContentState.Error, "今天加载失败", message);
            }
        }).ConfigureAwait(false);
    }

    private Task SetUnconfiguredStateAsync()
    {
        SetContentState(PageContentState.Error, "今天查询未配置", "任务查询服务尚未连接。");
        return Task.CompletedTask;
    }

    private static DateOnly GetTodayLocal(TimeProvider timeProvider)
    {
        var localNow = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow().ToUniversalTime(),
            timeProvider.LocalTimeZone);
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    private static ApplicationError? FirstError(params ApplicationError?[] errors)
    {
        return errors.FirstOrDefault(error => error is not null);
    }
}
