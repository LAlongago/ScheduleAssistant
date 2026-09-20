using CommunityToolkit.Mvvm.Input;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;
using ScheduleAssistant.Presentation.ViewModels;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class TodayAndDeadlineViewModelTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);
    private static readonly DateTimeOffset NowUtc = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TodayPage_WhenEntriesContainCombinedAndSameTitleTasks_ShouldGroupWithoutTitleDeduplication()
    {
        var useCases = new FakeTaskUseCases();
        var combined = CreateEntry(
            "计划与截止",
            plannedDate: Today,
            deadlineUtc: NowUtc.AddHours(3),
            isPlannedOnDate: true,
            isDeadlineOnDate: true,
            displayStatus: DisplayStatus.NotStarted,
            urgency: DeadlineUrgencyLevel.LessThanOneDay);
        var planOnly = CreateEntry(
            "同名",
            plannedDate: Today,
            isPlannedOnDate: true,
            displayStatus: DisplayStatus.NotStarted,
            urgency: DeadlineUrgencyLevel.None);
        var sameTitle = CreateEntry(
            "同名",
            plannedDate: Today,
            isPlannedOnDate: true,
            displayStatus: DisplayStatus.NotStarted,
            urgency: DeadlineUrgencyLevel.None);
        var overdue = CreateEntry(
            "已逾期",
            deadlineUtc: NowUtc.AddHours(-2),
            isDeadlineOnDate: true,
            displayStatus: DisplayStatus.Overdue,
            urgency: DeadlineUrgencyLevel.Overdue);
        var plannedPast = CreateEntry(
            "计划已过",
            plannedDate: Today.AddDays(-1),
            displayStatus: DisplayStatus.PlannedPast,
            urgency: DeadlineUrgencyLevel.None);
        var deadlineOnly = CreateEntry(
            "今日截止",
            deadlineUtc: NowUtc.AddHours(5),
            isDeadlineOnDate: true,
            displayStatus: DisplayStatus.NotStarted,
            urgency: DeadlineUrgencyLevel.LessThanOneDay);
        var completed = CreateEntry(
            "已完成",
            plannedDate: Today,
            isPlannedOnDate: true,
            displayStatus: DisplayStatus.Completed,
            urgency: DeadlineUrgencyLevel.None,
            workflowStatus: WorkflowStatus.Completed);

        useCases.TodayPending = Success(new TodayPendingDto(
            Today,
            new[] { overdue, plannedPast, combined, planOnly, sameTitle, deadlineOnly }));
        useCases.CalendarDay = Success(new CalendarDayDto(Today, true, new[] { completed }));
        useCases.Deadlines = Success(new DeadlineQueryResult(
            new[] { combined, deadlineOnly },
            new[] { overdue }));

        using var page = CreateTodayPage(useCases);
        await page.LoadAsync();

        Assert.Single(page.OverdueTasks);
        Assert.Single(page.PlannedPastTasks);
        Assert.Collection(page.PlannedTodayTasks, _ => { }, _ => { }, _ => { });
        Assert.Single(page.DeadlineTodayTasks);
        Assert.Single(page.CompletedTasks);
        Assert.Equal(2, page.PlannedTodayTasks.Count(card => card.Title == "同名"));

        var combinedCard = Assert.Single(page.PlannedTodayTasks, card => card.TaskId == combined.Task.Id);
        Assert.Contains("计划", combinedCard.PlanMarkerText);
        Assert.Contains("今日 Deadline", combinedCard.DeadlineMarkerText);
        Assert.Equal(combined.Task.Version, combinedCard.Version);
        Assert.NotNull(page.RecentDeadline);
        Assert.Equal(2, page.AdditionalDeadlines.Count);
        Assert.Equal(PageContentState.Ready, page.ContentState);
    }

    [Fact]
    public async Task UpcomingPage_WhenRangeChanges_ShouldLimitOptionsToSevenDaysAndKeepReturnedOrder()
    {
        var useCases = new FakeTaskUseCases();
        var weekFirst = CreateEntry("七天第一", deadlineUtc: NowUtc.AddDays(5), isDeadlineOnDate: true);
        var weekSecond = CreateEntry("七天第二", deadlineUtc: NowUtc.AddDays(6), isDeadlineOnDate: true);
        var rangeFirst = CreateEntry("范围第一", deadlineUtc: NowUtc.AddHours(10), isDeadlineOnDate: true);
        var rangeSecond = CreateEntry("范围第二", deadlineUtc: NowUtc.AddDays(2), isDeadlineOnDate: true);
        useCases.DeadlinesByRange[DeadlineQueryRange.Next7Days] = Success(
            new DeadlineQueryResult(new[] { weekFirst, weekSecond }, Array.Empty<CalendarEntry>()));
        useCases.DeadlinesByRange[DeadlineQueryRange.Next3Days] = Success(
            new DeadlineQueryResult(new[] { rangeFirst, rangeSecond }, Array.Empty<CalendarEntry>()));

        using var page = CreateUpcomingPage(useCases);
        await page.LoadAsync();
        await page.SelectRangeAsync(DeadlineQueryRange.Next3Days);

        Assert.Equal(
            new[] { DeadlineQueryRange.Next7Days, DeadlineQueryRange.Next3Days },
            useCases.DeadlineRanges);
        Assert.Equal(DeadlineQueryRange.Next3Days, page.SelectedRange);
        Assert.Equal("范围第一", page.UpcomingTasks[0].Title);
        Assert.Equal("范围第二", page.UpcomingTasks[1].Title);
        Assert.Single(page.RangeOptions, option => option.IsSelected && option.Value == DeadlineQueryRange.Next3Days);
        Assert.Equal(
            new[]
            {
                DeadlineQueryRange.Next24Hours,
                DeadlineQueryRange.Next3Days,
                DeadlineQueryRange.Next7Days
            },
            page.RangeOptions.Select(option => option.Value));
        Assert.Equal(PageContentState.Ready, page.ContentState);

        await page.SelectRangeAsync(DeadlineQueryRange.Next30Days);

        Assert.Equal(DeadlineQueryRange.Next3Days, page.SelectedRange);
        Assert.Equal(2, useCases.DeadlineRanges.Count);
    }

    [Fact]
    public async Task TaskCard_WhenCompletionSucceedsThenConflicts_ShouldUpdateVersionAndRollbackFailure()
    {
        var useCases = new FakeTaskUseCases();
        var pending = CreateEntry(
            "待完成",
            plannedDate: Today,
            isPlannedOnDate: true,
            displayStatus: DisplayStatus.NotStarted,
            urgency: DeadlineUrgencyLevel.None);
        var completed = pending.Task with
        {
            WorkflowStatus = WorkflowStatus.Completed,
            CompletedAtUtc = NowUtc,
            UpdatedAtUtc = NowUtc,
            Version = pending.Task.Version + 1
        };
        useCases.CompleteResult = Success(completed);
        var mapper = new TaskCardMapper(useCases, new InlineDispatcher());
        var card = mapper.Map(pending, new Dictionary<Guid, CategoryOptionDto>());
        var command = Assert.IsAssignableFrom<IAsyncRelayCommand<object?>>(card.CompletionCommand);

        await command.ExecuteAsync(true);

        Assert.True(card.IsCompleted);
        Assert.Equal(completed.Version, card.Version);
        Assert.Equal(pending.Task.Id, useCases.LastCompletedCommand!.TaskId);
        Assert.Equal(pending.Task.Version, useCases.LastCompletedCommand.ExpectedVersion);
        Assert.Equal(string.Empty, card.ErrorText);

        useCases.CancelCompletionResult = Failure<TaskDto>(
            new ApplicationError(ApplicationErrorKind.Conflict, "Task.VersionConflict", "任务已被其他窗口更新，请刷新后重试。"));
        await command.ExecuteAsync(false);

        Assert.True(card.IsCompleted);
        Assert.Equal(completed.Version, card.Version);
        Assert.Equal("任务已被其他窗口更新，请刷新后重试。", card.ErrorText);
        Assert.Equal(completed.Version, useCases.LastCancelledCommand!.ExpectedVersion);
    }

    [Fact]
    public async Task TodayPage_WhenTaskEventArrives_ShouldRefreshOnlySubscribedPageData()
    {
        var useCases = new FakeTaskUseCases();
        using var eventBus = new EventBusLifetime();
        using var page = CreateTodayPage(useCases, eventBus.Bus);
        await page.LoadAsync();
        var initialTodayCalls = useCases.TodayPendingCalls;
        var initialCalendarCalls = useCases.CalendarDateCalls;
        var initialDeadlineCalls = useCases.DeadlineCalls;

        await eventBus.Bus.PublishAsync(new TaskUpdated(
            Guid.NewGuid(),
            2,
            new[] { Today },
            DeadlineChanged: true));

        Assert.Equal(initialTodayCalls + 1, useCases.TodayPendingCalls);
        Assert.Equal(initialCalendarCalls + 1, useCases.CalendarDateCalls);
        Assert.Equal(initialDeadlineCalls + 1, useCases.DeadlineCalls);

        await eventBus.Bus.PublishAsync(new ReminderPlanChanged(
            Guid.NewGuid(),
            3,
            new[] { Today },
            HasPendingPlan: true));

        Assert.Equal(initialTodayCalls + 1, useCases.TodayPendingCalls);
        Assert.Equal(initialCalendarCalls + 1, useCases.CalendarDateCalls);
        Assert.Equal(initialDeadlineCalls + 1, useCases.DeadlineCalls);
    }

    [Fact]
    public async Task TodayPage_WhenMinuteTickArrives_ShouldUpdateCachedCountdownWithoutQuerying()
    {
        var useCases = new FakeTaskUseCases();
        var deadline = CreateEntry(
            "缓存倒计时",
            deadlineUtc: NowUtc.AddHours(2),
            isDeadlineOnDate: true,
            urgency: DeadlineUrgencyLevel.LessThanOneDay);
        useCases.Deadlines = Success(new DeadlineQueryResult(new[] { deadline }, Array.Empty<CalendarEntry>()));
        var timeProvider = new MutableTimeProvider(NowUtc);
        var timer = new FakeDeadlineRefreshTimer();
        using var page = CreateTodayPage(useCases, timeProvider: timeProvider, timer: timer);
        await page.LoadAsync();
        var initialTodayCalls = useCases.TodayPendingCalls;
        var initialCalendarCalls = useCases.CalendarDateCalls;
        var initialDeadlineCalls = useCases.DeadlineCalls;
        var initialText = page.RecentDeadline!.RemainingText;

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await timer.RaiseMinuteAsync();

        Assert.NotEqual(initialText, page.RecentDeadline.RemainingText);
        Assert.Equal(initialTodayCalls, useCases.TodayPendingCalls);
        Assert.Equal(initialCalendarCalls, useCases.CalendarDateCalls);
        Assert.Equal(initialDeadlineCalls, useCases.DeadlineCalls);
    }

    private static TodayPageViewModel CreateTodayPage(
        FakeTaskUseCases useCases,
        InProcessEventBus? eventBus = null,
        MutableTimeProvider? timeProvider = null,
        FakeDeadlineRefreshTimer? timer = null)
    {
        return new TodayPageViewModel(
            useCases,
            useCases,
            new TaskCardMapper(useCases, new InlineDispatcher()),
            new FakeDatabaseInitialization(),
            eventBus ?? new InProcessEventBus(),
            new InlineDispatcher(),
            timeProvider ?? new MutableTimeProvider(NowUtc),
            timer ?? new FakeDeadlineRefreshTimer());
    }

    private static UpcomingDeadlinesPageViewModel CreateUpcomingPage(FakeTaskUseCases useCases)
    {
        return new UpcomingDeadlinesPageViewModel(
            useCases,
            useCases,
            new TaskCardMapper(useCases, new InlineDispatcher()),
            new FakeDatabaseInitialization(),
            new InProcessEventBus(),
            new InlineDispatcher());
    }

    private static CalendarEntry CreateEntry(
        string title,
        DateOnly? plannedDate = null,
        DateTimeOffset? deadlineUtc = null,
        bool isPlannedOnDate = false,
        bool isDeadlineOnDate = false,
        DisplayStatus displayStatus = DisplayStatus.NotStarted,
        DeadlineUrgencyLevel urgency = DeadlineUrgencyLevel.None,
        WorkflowStatus workflowStatus = WorkflowStatus.Pending)
    {
        var task = new TaskDto(
            Guid.NewGuid(),
            title,
            Guid.Empty,
            TaskPriority.Normal,
            workflowStatus,
            plannedDate,
            plannedDate.HasValue ? new TimeOnly(10, 0) : null,
            null,
            deadlineUtc.HasValue
                ? new DeadlineDto(
                    DateOnly.FromDateTime(deadlineUtc.Value.UtcDateTime),
                    TimeOnly.FromDateTime(deadlineUtc.Value.UtcDateTime),
                    "UTC",
                    deadlineUtc.Value)
                : null,
            "办公室",
            null,
            null,
            null,
            null,
            null,
            false,
            NowUtc.AddDays(-1),
            NowUtc,
            workflowStatus == WorkflowStatus.Completed ? NowUtc : null,
            1);
        return new CalendarEntry(task, Today, isPlannedOnDate, isDeadlineOnDate, displayStatus, urgency);
    }

    private static ApplicationResult<T> Success<T>(T value) => ApplicationResult<T>.Success(value);

    private static ApplicationResult<T> Failure<T>(ApplicationError error) => ApplicationResult<T>.Failure(error);

    private sealed class FakeTaskUseCases : ITaskUseCases
    {
        public ApplicationResult<TodayPendingDto> TodayPending { get; set; } = Success(
            new TodayPendingDto(Today, Array.Empty<CalendarEntry>()));

        public ApplicationResult<CalendarDayDto> CalendarDay { get; set; } = Success(
            new CalendarDayDto(Today, true, Array.Empty<CalendarEntry>()));

        public ApplicationResult<DeadlineQueryResult> Deadlines { get; set; } = Success(
            new DeadlineQueryResult(Array.Empty<CalendarEntry>(), Array.Empty<CalendarEntry>()));

        public Dictionary<DeadlineQueryRange, ApplicationResult<DeadlineQueryResult>> DeadlinesByRange { get; } = new();

        public IReadOnlyList<CategoryOptionDto> Categories { get; set; } = new[]
        {
            new CategoryOptionDto(Guid.Empty, "其他", "#667085", 0)
        };

        public List<DeadlineQueryRange> DeadlineRanges { get; } = new();

        public int TodayPendingCalls { get; private set; }

        public int CalendarDateCalls { get; private set; }

        public int DeadlineCalls { get; private set; }

        public ChangeTaskStateCommand? LastCompletedCommand { get; private set; }

        public ChangeTaskStateCommand? LastCancelledCommand { get; private set; }

        public ApplicationResult<TaskDto> CompleteResult { get; set; } = Failure<TaskDto>(
            new ApplicationError(ApplicationErrorKind.Unexpected, "Test.NotConfigured", "测试未配置完成结果。"));

        public ApplicationResult<TaskDto> CancelCompletionResult { get; set; } = Failure<TaskDto>(
            new ApplicationError(ApplicationErrorKind.Unexpected, "Test.NotConfigured", "测试未配置取消完成结果。"));

        public Task<ApplicationResult<TaskDto>> CreateAsync(CreateTaskCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(Failure<TaskDto>(UnsupportedError()));

        public Task<ApplicationResult<TaskDto>> UpdateAsync(UpdateTaskCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(Failure<TaskDto>(UnsupportedError()));

        public Task<ApplicationResult<TaskDto>> CompleteAsync(ChangeTaskStateCommand command, CancellationToken cancellationToken = default)
        {
            LastCompletedCommand = command;
            return Task.FromResult(CompleteResult);
        }

        public Task<ApplicationResult<TaskDto>> CancelCompletionAsync(ChangeTaskStateCommand command, CancellationToken cancellationToken = default)
        {
            LastCancelledCommand = command;
            return Task.FromResult(CancelCompletionResult);
        }

        public Task<ApplicationResult<TaskDto>> StartProcessingAsync(ChangeTaskStateCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(Failure<TaskDto>(UnsupportedError()));

        public Task<ApplicationResult<DeletedTaskDto>> DeleteAsync(DeleteTaskCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(Failure<DeletedTaskDto>(UnsupportedError()));

        public Task<ApplicationResult<TaskDto>> GetAsync(GetTaskQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Failure<TaskDto>(UnsupportedError()));

        public Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetPlannedByDateAsync(
            GetTasksByPlannedDateQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Failure<IReadOnlyList<TaskDto>>(UnsupportedError()));

        public Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetUpcomingDeadlinesAsync(
            GetUpcomingDeadlinesQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Failure<IReadOnlyList<TaskDto>>(UnsupportedError()));

        public Task<ApplicationResult<IReadOnlyList<CategoryOptionDto>>> GetCategoryOptionsAsync(
            GetCategoryOptionsQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Success(Categories));

        public Task<ApplicationResult<CalendarDayDto>> GetCalendarDateAsync(
            GetCalendarDateQuery query,
            CancellationToken cancellationToken = default)
        {
            CalendarDateCalls++;
            return Task.FromResult(CalendarDay);
        }

        public Task<ApplicationResult<TodayPendingDto>> GetTodayPendingAsync(
            GetTodayPendingQuery query,
            CancellationToken cancellationToken = default)
        {
            TodayPendingCalls++;
            return Task.FromResult(TodayPending);
        }

        public Task<ApplicationResult<WeekCalendarDto>> GetWeekCalendarAsync(
            GetWeekCalendarQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Failure<WeekCalendarDto>(UnsupportedError()));

        public Task<ApplicationResult<MonthCalendarDto>> GetMonthCalendarAsync(
            GetMonthCalendarQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Failure<MonthCalendarDto>(UnsupportedError()));

        public Task<ApplicationResult<DeadlineQueryResult>> GetDeadlinesAsync(
            GetDeadlinesQuery query,
            CancellationToken cancellationToken = default)
        {
            DeadlineCalls++;
            DeadlineRanges.Add(query.Range);
            return Task.FromResult(DeadlinesByRange.GetValueOrDefault(query.Range, Deadlines));
        }

        public Task<ApplicationResult<PagedResult<TaskDto>>> SearchAsync(
            SearchTasksQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Failure<PagedResult<TaskDto>>(UnsupportedError()));

        private static ApplicationError UnsupportedError()
            => new(ApplicationErrorKind.Unexpected, "Test.Unsupported", "测试未配置此调用。");
    }

    private sealed class FakeDatabaseInitialization : IDatabaseInitialization
    {
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess => true;

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }

    private sealed class FakeDeadlineRefreshTimer : IDeadlineRefreshTimer
    {
        private Func<Task>? _onMinuteTick;
        private Func<Task>? _onDeadlineReached;

        public void Start(Func<Task> onMinuteTick, Func<Task> onDeadlineReached)
        {
            _onMinuteTick = onMinuteTick;
            _onDeadlineReached = onDeadlineReached;
        }

        public void ScheduleDeadline(DateTimeOffset? deadlineUtc)
        {
            LastScheduledDeadline = deadlineUtc;
        }

        public DateTimeOffset? LastScheduledDeadline { get; private set; }

        public Task RaiseMinuteAsync() => _onMinuteTick?.Invoke() ?? Task.CompletedTask;

        public Task RaiseDeadlineAsync() => _onDeadlineReached?.Invoke() ?? Task.CompletedTask;

        public void Dispose()
        {
            _onMinuteTick = null;
            _onDeadlineReached = null;
        }
    }

    private sealed class EventBusLifetime : IDisposable
    {
        public InProcessEventBus Bus { get; } = new();

        public void Dispose()
        {
        }
    }
}
