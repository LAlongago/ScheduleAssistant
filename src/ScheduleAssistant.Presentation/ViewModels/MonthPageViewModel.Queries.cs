using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

public sealed partial class MonthPageViewModel
{
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_queries is null || _useCases is null || _cardMapper is null
            || _databaseInitialization is null || _dispatcher is null || _disposed)
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
            // A completed refresh may already have released its source.
        }

        try
        {
            await _refreshGate.WaitAsync(refreshCancellation.Token).ConfigureAwait(false);
            try
            {
                await InvokeOnUiAsync(() =>
                {
                    SetContentState(PageContentState.Loading, "正在加载月计划", "正在读取本地任务数据。");
                }).ConfigureAwait(false);

                var month = DisplayedMonth;
                await _databaseInitialization.EnsureInitializedAsync(refreshCancellation.Token)
                    .ConfigureAwait(false);
                var monthTask = _queries.GetMonthCalendarAsync(
                    new GetMonthCalendarQuery(month),
                    refreshCancellation.Token);
                var categoriesTask = LoadCategoriesAsync(refreshCancellation.Token);
                await Task.WhenAll(monthTask, categoriesTask).ConfigureAwait(false);
                refreshCancellation.Token.ThrowIfCancellationRequested();

                var monthResult = await monthTask.ConfigureAwait(false);
                var categoriesResult = await categoriesTask.ConfigureAwait(false);
                var error = FirstError(monthResult.Error, categoriesResult.Error);
                if (error is not null || monthResult.Value is null || categoriesResult.Value is null)
                {
                    await SetPageErrorAsync(
                        error?.Message ?? "月历数据暂时无法读取，请稍后重试。",
                        refreshCancellation.Token).ConfigureAwait(false);
                    return;
                }

                var categories = categoriesResult.Value.ToDictionary(category => category.Id);
                DateOnly? selectedDateToRefresh = null;
                await InvokeOnUiAsync(() =>
                {
                    if (_disposed || DisplayedMonth != month)
                    {
                        return;
                    }

                    ApplyMonthResult(monthResult.Value, categories);
                    if (IsDateDetailsOpen && SelectedDate.HasValue)
                    {
                        selectedDateToRefresh = SelectedDate.Value;
                    }
                }).ConfigureAwait(false);

                if (selectedDateToRefresh.HasValue)
                {
                    await LoadDateDetailsCoreAsync(
                        selectedDateToRefresh.Value,
                        showLoading: false,
                        cancellationToken: refreshCancellation.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                try
                {
                    _refreshGate.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Disposal may race a cancelled refresh during shutdown.
                }
            }
        }
        catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested || _disposed)
        {
            // A newer navigation/event refresh or page disposal superseded this query.
        }
        catch
        {
            await SetPageErrorAsync("月历数据暂时无法读取，请稍后重试。", CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            Interlocked.CompareExchange(ref _refreshCancellation, null, refreshCancellation);
            refreshCancellation.Dispose();
        }
    }

    private async Task<ApplicationResult<IReadOnlyList<CategoryOptionDto>>> LoadCategoriesAsync(
        CancellationToken cancellationToken)
    {
        if (_useCases is null)
        {
            return ApplicationResult<IReadOnlyList<CategoryOptionDto>>.Failure(
                new ApplicationError(
                    ApplicationErrorKind.Unexpected,
                    "Presentation.NotConfigured",
                    "任务分类服务尚未连接。"));
        }

        if (_categories.Count > 0)
        {
            return ApplicationResult<IReadOnlyList<CategoryOptionDto>>.Success(_categories.Values.ToArray());
        }

        return await _useCases.GetCategoryOptionsAsync(
            new GetCategoryOptionsQuery(),
            cancellationToken).ConfigureAwait(false);
    }

    private void ApplyMonthResult(
        MonthCalendarDto month,
        IReadOnlyDictionary<Guid, CategoryOptionDto> categories)
    {
        if (month.Days.Count != CalendarDayCount)
        {
            throw new InvalidOperationException("The month query did not return a fixed 42-day grid.");
        }

        _categories.Clear();
        foreach (var category in categories.Values)
        {
            _categories[category.Id] = category;
        }

        _days.Clear();
        var today = _timeProvider is null
            ? DateOnly.FromDateTime(DateTime.Today)
            : GetTodayLocal(_timeProvider);
        foreach (var day in month.Days)
        {
            _days.Add(new MonthCalendarDayViewModel(day, day.Date == today, _cardMapper!, categories));
        }

        OnPropertyChanged(nameof(HasEntries));
        var hasEntries = HasEntries;
        SetContentState(
            hasEntries ? PageContentState.Ready : PageContentState.Empty,
            hasEntries ? DisplayedMonthText : "本月没有安排",
            hasEntries ? "月历数据已加载。" : "当前 42 个日期格没有计划或 Deadline。选择其他月份继续查看。");
    }

    private Task LoadDateDetailsAsync(DateOnly date)
    {
        _lastDateDetailsLoadTask = StartDateDetailsLoadAsync(date);
        return _lastDateDetailsLoadTask;
    }

    private async Task StartDateDetailsLoadAsync(DateOnly date)
    {
        using var detailsCancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref _dateDetailsCancellation, detailsCancellation);
        try
        {
            previousCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A completed details query may already have released its source.
        }

        try
        {
            await LoadDateDetailsCoreAsync(
                date,
                showLoading: true,
                cancellationToken: detailsCancellation.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            Interlocked.CompareExchange(ref _dateDetailsCancellation, null, detailsCancellation);
        }
    }

    private async Task LoadDateDetailsCoreAsync(
        DateOnly date,
        bool showLoading,
        CancellationToken cancellationToken)
    {
        if (_queries is null || _useCases is null || _cardMapper is null
            || _databaseInitialization is null || _dispatcher is null || _disposed)
        {
            await SetDateDetailsErrorAsync("日期详情查询服务尚未连接。", CancellationToken.None)
                .ConfigureAwait(false);
            return;
        }

        if (showLoading)
        {
            await InvokeOnUiAsync(() =>
            {
                SelectedDate = date;
                IsDateDetailsOpen = true;
                SetDateDetailsState(PageContentState.Loading, "正在加载日期详情", "正在读取当天的完整事项。");
            }).ConfigureAwait(false);
        }

        try
        {
            await _databaseInitialization.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            var dateTask = _queries.GetCalendarDateAsync(
                new GetCalendarDateQuery(date),
                cancellationToken);
            var categoriesTask = LoadCategoriesAsync(cancellationToken);
            await Task.WhenAll(dateTask, categoriesTask).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var dateResult = await dateTask.ConfigureAwait(false);
            var categoriesResult = await categoriesTask.ConfigureAwait(false);
            var error = FirstError(dateResult.Error, categoriesResult.Error);
            if (error is not null || dateResult.Value is null || categoriesResult.Value is null)
            {
                await SetDateDetailsErrorAsync(
                    error?.Message ?? "日期详情暂时无法读取，请稍后重试。",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var categories = categoriesResult.Value.ToDictionary(category => category.Id);
            await InvokeOnUiAsync(() =>
            {
                if (_disposed || !IsDateDetailsOpen || SelectedDate != date)
                {
                    return;
                }

                _categories.Clear();
                foreach (var category in categories.Values)
                {
                    _categories[category.Id] = category;
                }

                _dateDetails.Clear();
                foreach (var entry in dateResult.Value.Entries)
                {
                    _dateDetails.Add(_cardMapper.Map(entry, categories, TaskCardMode.Compact));
                }

                SetDateDetailsState(
                    _dateDetails.Count > 0 ? PageContentState.Ready : PageContentState.Empty,
                    _dateDetails.Count > 0 ? SelectedDateText : "当天没有安排",
                    _dateDetails.Count > 0
                        ? "当天完整事项已加载。"
                        : "该日期没有计划或 Deadline。");
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _disposed)
        {
            // A newer date selection or page disposal superseded this query.
        }
        catch
        {
            await SetDateDetailsErrorAsync(
                "日期详情暂时无法读取，请稍后重试。",
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private void CloseDateDetails()
    {
        try
        {
            _dateDetailsCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The previous details query may have completed concurrently.
        }

        IsDateDetailsOpen = false;
        SelectedDate = null;
        _dateDetails.Clear();
        SetDateDetailsState(PageContentState.Empty, "选择日期", "点击日期查看当天的完整事项。");
    }

    private Task OnApplicationEventAsync(
        TaskApplicationEvent applicationEvent,
        CancellationToken cancellationToken)
    {
        if (_disposed
            || applicationEvent is ReminderPlanChanged
            || !applicationEvent.AffectedDates.Any(IsInCurrentGrid))
        {
            return Task.CompletedTask;
        }

        return LoadAsync(cancellationToken);
    }

    private bool IsInCurrentGrid(DateOnly date)
    {
        return date >= GridStart && date <= GridEnd;
    }

    private async Task SetPageErrorAsync(string message, CancellationToken cancellationToken)
    {
        await InvokeOnUiAsync(() =>
        {
            if (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                SetContentState(PageContentState.Error, "月历加载失败", message);
            }
        }).ConfigureAwait(false);
    }

    private async Task SetDateDetailsErrorAsync(string message, CancellationToken cancellationToken)
    {
        await InvokeOnUiAsync(() =>
        {
            if (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                IsDateDetailsOpen = true;
                SetDateDetailsState(PageContentState.Error, "日期详情加载失败", message);
            }
        }).ConfigureAwait(false);
    }

    private Task SetUnconfiguredStateAsync()
    {
        SetContentState(PageContentState.Error, "月计划查询未配置", "任务查询服务尚未连接。");
        return Task.CompletedTask;
    }

    private Task InvokeOnUiAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _dispatcher is null ? RunActionAsync(action) : _dispatcher.InvokeAsync(action);
    }

    private static Task RunActionAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private void SetDateDetailsState(
        PageContentState state,
        string statusTitle,
        string statusMessage)
    {
        DateDetailsState = state;
        DateDetailsStatusTitle = statusTitle;
        DateDetailsStatusMessage = statusMessage;
    }

    private static ApplicationError? FirstError(params ApplicationError?[] errors)
    {
        return errors.FirstOrDefault(error => error is not null);
    }
}
