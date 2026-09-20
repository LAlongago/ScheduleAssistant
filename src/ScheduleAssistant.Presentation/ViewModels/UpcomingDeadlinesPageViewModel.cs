using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Selectable Deadline range shown by the upcoming-deadlines page.</summary>
public sealed class DeadlineRangeOptionViewModel : ObservableObject
{
    private bool _isSelected;

    /// <summary>Initializes one range option.</summary>
    public DeadlineRangeOptionViewModel(DeadlineQueryRange value, string label)
    {
        Value = value;
        Label = label;
    }

    /// <summary>Gets the Application range value.</summary>
    public DeadlineQueryRange Value { get; }

    /// <summary>Gets the localized range label.</summary>
    public string Label { get; }

    /// <summary>Gets whether this option is selected.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        private set => SetProperty(ref _isSelected, value);
    }

    internal void SetSelected(bool selected) => IsSelected = selected;
}

/// <summary>Real upcoming-Deadline query, range selection, and local-refresh state.</summary>
public sealed class UpcomingDeadlinesPageViewModel : PageViewModelBase, IDisposable
{
    private readonly ITaskQueries? _queries;
    private readonly ITaskUseCases? _useCases;
    private readonly ITaskCardMapper? _cardMapper;
    private readonly IDatabaseInitialization? _databaseInitialization;
    private readonly InProcessEventBus? _eventBus;
    private readonly IUiDispatcher? _dispatcher;
    private readonly Dictionary<Guid, CategoryOptionDto> _categories = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IDisposable? _eventSubscription;
    private CancellationTokenSource? _refreshCancellation;
    private Task _lastLoadTask = Task.CompletedTask;
    private DeadlineQueryRange _selectedRange = DeadlineQueryRange.All;
    private bool _disposed;

    /// <summary>Creates an inert instance for shell unit tests that do not load data.</summary>
    public UpcomingDeadlinesPageViewModel()
        : base("即将截止", "按 Deadline 和紧迫度聚合待处理事项", PageContentState.Loading, "正在加载 Deadline", "正在读取本地任务数据。")
    {
        RangeOptions = CreateRangeOptions();
        SelectRangeCommand = new AsyncRelayCommand<DeadlineQueryRange>(SelectRangeAsync);
    }

    /// <summary>Creates a real Deadline page connected to Application ports and local services.</summary>
    public UpcomingDeadlinesPageViewModel(
        ITaskQueries queries,
        ITaskUseCases useCases,
        ITaskCardMapper cardMapper,
        IDatabaseInitialization databaseInitialization,
        InProcessEventBus eventBus,
        IUiDispatcher dispatcher)
        : base("即将截止", "按 Deadline 和紧迫度聚合待处理事项", PageContentState.Loading, "正在加载 Deadline", "正在读取本地任务数据。")
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _useCases = useCases ?? throw new ArgumentNullException(nameof(useCases));
        _cardMapper = cardMapper ?? throw new ArgumentNullException(nameof(cardMapper));
        _databaseInitialization = databaseInitialization ?? throw new ArgumentNullException(nameof(databaseInitialization));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        RangeOptions = CreateRangeOptions();
        SelectRangeCommand = new AsyncRelayCommand<DeadlineQueryRange>(SelectRangeAsync);
        _eventSubscription = _eventBus.Subscribe<TaskApplicationEvent>(OnApplicationEventAsync);
    }

    /// <summary>Gets the overdue entries returned by the Application query.</summary>
    public ObservableCollection<TaskCardViewModel> OverdueTasks { get; } = new();

    /// <summary>Gets upcoming entries in the exact Application-provided order.</summary>
    public ObservableCollection<TaskCardViewModel> UpcomingTasks { get; } = new();

    /// <summary>Gets the supported range choices.</summary>
    public IReadOnlyList<DeadlineRangeOptionViewModel> RangeOptions { get; }

    /// <summary>Gets the selected Application range.</summary>
    public DeadlineQueryRange SelectedRange
    {
        get => _selectedRange;
        private set => SetProperty(ref _selectedRange, value);
    }

    /// <summary>Gets a localized label for the selected range.</summary>
    public string SelectedRangeText => RangeOptions.First(option => option.Value == SelectedRange).Label;

    /// <summary>Gets whether overdue entries exist.</summary>
    public bool HasOverdueTasks => OverdueTasks.Count > 0;

    /// <summary>Gets whether upcoming entries exist.</summary>
    public bool HasUpcomingTasks => UpcomingTasks.Count > 0;

    /// <summary>Gets whether any Deadline content exists.</summary>
    public bool HasNormalContent => HasOverdueTasks || HasUpcomingTasks;

    /// <summary>Gets the last load task, useful for deterministic page tests.</summary>
    public Task LastLoadTask => _lastLoadTask;

    /// <summary>Gets the command bound to range buttons.</summary>
    public IAsyncRelayCommand<DeadlineQueryRange> SelectRangeCommand { get; }

    /// <summary>Loads the selected Deadline range asynchronously.</summary>
    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _lastLoadTask = _queries is null ? SetUnconfiguredStateAsync() : RefreshAsync(cancellationToken);
        return _lastLoadTask;
    }

    /// <summary>Selects a supported range and refreshes only the Deadline query.</summary>
    public Task SelectRangeAsync(DeadlineQueryRange range)
    {
        if (!Enum.IsDefined(range))
        {
            return Task.CompletedTask;
        }

        SelectedRange = range;
        foreach (var option in RangeOptions)
        {
            option.SetSelected(option.Value == range);
        }

        OnPropertyChanged(nameof(SelectedRangeText));
        return LoadAsync();
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

    private async Task RefreshAsync(CancellationToken cancellationToken)
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
                    SetContentState(PageContentState.Loading, "正在加载 Deadline", "正在读取本地任务数据。");
                }).ConfigureAwait(false);

                await _databaseInitialization.EnsureInitializedAsync(refreshCancellation.Token).ConfigureAwait(false);
                var deadlinesTask = _queries.GetDeadlinesAsync(
                    new GetDeadlinesQuery(SelectedRange),
                    refreshCancellation.Token);
                var categoriesTask = _categories.Count == 0
                    ? _useCases.GetCategoryOptionsAsync(new GetCategoryOptionsQuery(), refreshCancellation.Token)
                    : Task.FromResult(ApplicationResult<IReadOnlyList<CategoryOptionDto>>.Success(_categories.Values.ToArray()));

                await Task.WhenAll(deadlinesTask, categoriesTask).ConfigureAwait(false);
                refreshCancellation.Token.ThrowIfCancellationRequested();
                var deadlines = await deadlinesTask.ConfigureAwait(false);
                var categories = await categoriesTask.ConfigureAwait(false);
                var error = FirstError(deadlines.Error, categories.Error);
                if (error is not null || deadlines.Value is null || categories.Value is null)
                {
                    await SetErrorAsync(error?.Message ?? "Deadline 数据暂时无法读取，请稍后重试。", refreshCancellation.Token)
                        .ConfigureAwait(false);
                    return;
                }

                await _dispatcher.InvokeAsync(() => ApplyResults(deadlines.Value, categories.Value)).ConfigureAwait(false);
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
            await SetErrorAsync("Deadline 数据暂时无法读取，请稍后重试。", CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            Interlocked.CompareExchange(ref _refreshCancellation, null, refreshCancellation);
            refreshCancellation.Dispose();
        }
    }

    private void ApplyResults(DeadlineQueryResult deadlines, IReadOnlyList<CategoryOptionDto> categories)
    {
        _categories.Clear();
        foreach (var category in categories)
        {
            _categories[category.Id] = category;
        }

        OverdueTasks.Clear();
        UpcomingTasks.Clear();
        foreach (var entry in deadlines.Overdue)
        {
            OverdueTasks.Add(_cardMapper!.Map(entry, _categories));
        }

        foreach (var entry in deadlines.Upcoming)
        {
            UpcomingTasks.Add(_cardMapper!.Map(entry, _categories));
        }

        OnPropertyChanged(nameof(HasOverdueTasks));
        OnPropertyChanged(nameof(HasUpcomingTasks));
        OnPropertyChanged(nameof(HasNormalContent));
        var hasContent = HasNormalContent;
        SetContentState(
            hasContent ? PageContentState.Ready : PageContentState.Empty,
            hasContent ? "即将截止" : "暂时没有可展示的 Deadline",
            hasContent ? "Deadline 数据已加载。" : "当前范围内没有未完成的 Deadline。");
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
                SetContentState(PageContentState.Error, "Deadline 加载失败", message);
            }
        }).ConfigureAwait(false);
    }

    private Task SetUnconfiguredStateAsync()
    {
        SetContentState(PageContentState.Error, "Deadline 查询未配置", "任务查询服务尚未连接。");
        return Task.CompletedTask;
    }

    private static DeadlineRangeOptionViewModel[] CreateRangeOptions()
    {
        var options = new[]
        {
            new DeadlineRangeOptionViewModel(DeadlineQueryRange.Next24Hours, "24 小时"),
            new DeadlineRangeOptionViewModel(DeadlineQueryRange.Next3Days, "3 天"),
            new DeadlineRangeOptionViewModel(DeadlineQueryRange.Next7Days, "7 天"),
            new DeadlineRangeOptionViewModel(DeadlineQueryRange.Next30Days, "30 天"),
            new DeadlineRangeOptionViewModel(DeadlineQueryRange.All, "全部")
        };
        options[^1].SetSelected(true);
        return options;
    }

    private static ApplicationError? FirstError(params ApplicationError?[] errors)
    {
        return errors.FirstOrDefault(error => error is not null);
    }
}
