using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;
using ScheduleAssistant.Presentation.ViewModels;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class WeekPageViewModelTests
{
    private static readonly DateOnly WeekStart = new(2026, 9, 14);
    private static readonly DateOnly Today = new(2026, 9, 20);
    private static readonly DateTimeOffset NowUtc = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] WeekdayLabels = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
    private static readonly string[] OrderedEntryTitles = { "计划事项", "截止事项", "计划并截止" };

    [Fact]
    public async Task LoadAsync_WhenApplicationReturnsWeek_ShouldMapMondayThroughSundayInOrder()
    {
        var useCases = new FakeTaskUseCases
        {
            WeekCalendar = Success(CreateWeek())
        };

        using var page = CreatePage(useCases);
        await page.LoadAsync();

        Assert.Equal(PageContentState.Empty, page.ContentState);
        Assert.Equal(7, page.WeekDays.Count);
        Assert.Equal(
            Enumerable.Range(0, 7).Select(WeekStart.AddDays),
            page.WeekDays.Select(day => day.Date));
        Assert.Equal(
            WeekdayLabels,
            page.WeekDays.Select(day => day.WeekdayText));
        Assert.Equal("9月14日", page.WeekDays[0].DateText);
        Assert.Equal("9月20日", page.WeekDays[6].DateText);
        Assert.True(page.WeekDays[6].IsToday);
        Assert.All(page.WeekDays, day => Assert.Empty(day.Tasks));
        Assert.Equal(WeekStart, useCases.RequestedWeekStarts.Single());
    }

    [Fact]
    public async Task LoadAsync_WhenEntriesShareADay_ShouldKeepPlanDeadlineAndCombinedMarkers()
    {
        var planOnly = CreateEntry("计划事项", WeekStart, isPlannedOnDate: true);
        var deadlineOnly = CreateEntry("截止事项", WeekStart, isDeadlineOnDate: true);
        var combined = CreateEntry(
            "计划并截止",
            WeekStart,
            isPlannedOnDate: true,
            isDeadlineOnDate: true);
        var useCases = new FakeTaskUseCases
        {
            WeekCalendar = Success(CreateWeek((0, new[] { planOnly, deadlineOnly, combined })))
        };

        using var page = CreatePage(useCases);
        await page.LoadAsync();

        var cards = page.WeekDays[0].Tasks;
        Assert.Equal(3, cards.Count);
        Assert.Equal(OrderedEntryTitles, cards.Select(card => card.Title));
        Assert.Contains(cards, card => card.Title == "计划事项" && card.PlanMarkerText.Contains("计划"));
        Assert.Contains(cards, card => card.Title == "截止事项" && card.DeadlineMarkerText.Contains("Deadline"));
        var combinedCard = Assert.Single(cards, card => card.Title == "计划并截止");
        Assert.Contains("●", combinedCard.PlanMarkerText);
        Assert.Contains("◆", combinedCard.DeadlineMarkerText);
        Assert.Equal(TaskCardMode.Compact, combinedCard.Mode);
    }

    [Fact]
    public async Task WeekNavigationCommands_WhenInvoked_ShouldMoveBySevenDaysAndReturnToCurrentWeek()
    {
        var useCases = new FakeTaskUseCases
        {
            WeekCalendar = Success(CreateWeek())
        };
        var timeProvider = new FixedTimeProvider(NowUtc);
        using var page = CreatePage(useCases, timeProvider: timeProvider);

        await page.LoadAsync();
        await page.PreviousWeekCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 9, 7), page.WeekStart);

        await page.NextWeekCommand.ExecuteAsync(null);
        Assert.Equal(WeekStart, page.WeekStart);

        await page.NextWeekCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 9, 21), page.WeekStart);

        await page.CurrentWeekCommand.ExecuteAsync(null);
        Assert.Equal(WeekStart, page.WeekStart);
        Assert.Equal(
            new[]
            {
                WeekStart,
                new DateOnly(2026, 9, 7),
                WeekStart,
                new DateOnly(2026, 9, 21),
                WeekStart
            },
            useCases.RequestedWeekStarts);
    }

    [Fact]
    public async Task LoadAsync_WhenWeekQueryFails_ShouldExposeErrorState()
    {
        var useCases = new FakeTaskUseCases
        {
            WeekCalendar = Failure<WeekCalendarDto>(
                new ApplicationError(
                    ApplicationErrorKind.StorageUnavailable,
                    "Calendar.Unavailable",
                    "本地任务数据暂时无法读取。"))
        };

        using var page = CreatePage(useCases);
        await page.LoadAsync();

        Assert.Equal(PageContentState.Error, page.ContentState);
        Assert.Equal("周计划加载失败", page.StatusTitle);
        Assert.Equal("本地任务数据暂时无法读取。", page.StatusMessage);
    }

    [Fact]
    public async Task ApplicationEvent_WhenAffectedDateIsInCurrentWeek_ShouldRefreshWeekOnly()
    {
        var useCases = new FakeTaskUseCases
        {
            WeekCalendar = Success(CreateWeek())
        };
        var eventBus = new InProcessEventBus();
        using var page = CreatePage(useCases, eventBus);
        await page.LoadAsync();
        var initialCalls = useCases.WeekCalendarCalls;

        await eventBus.PublishAsync(new TaskUpdated(
            Guid.NewGuid(),
            2,
            new[] { Today },
            DeadlineChanged: true));
        Assert.Equal(initialCalls + 1, useCases.WeekCalendarCalls);

        await eventBus.PublishAsync(new TaskUpdated(
            Guid.NewGuid(),
            3,
            new[] { Today.AddDays(-8) },
            DeadlineChanged: true));
        Assert.Equal(initialCalls + 1, useCases.WeekCalendarCalls);

        await eventBus.PublishAsync(new ReminderPlanChanged(
            Guid.NewGuid(),
            4,
            new[] { Today },
            HasPendingPlan: true));
        Assert.Equal(initialCalls + 1, useCases.WeekCalendarCalls);
    }

    private static WeekPageViewModel CreatePage(
        FakeTaskUseCases useCases,
        InProcessEventBus? eventBus = null,
        FixedTimeProvider? timeProvider = null)
    {
        return new WeekPageViewModel(
            useCases,
            useCases,
            new TaskCardMapper(useCases, new InlineDispatcher()),
            new FakeDatabaseInitialization(),
            eventBus ?? new InProcessEventBus(),
            new InlineDispatcher(),
            timeProvider ?? new FixedTimeProvider(NowUtc));
    }

    private static WeekCalendarDto CreateWeek(params (int DayOffset, IReadOnlyList<CalendarEntry> Entries)[] populatedDays)
    {
        var entriesByDay = populatedDays.ToDictionary(item => item.DayOffset, item => item.Entries);
        var days = Enumerable.Range(0, 7)
            .Select(offset => new CalendarDayDto(
                WeekStart.AddDays(offset),
                IsInDisplayedMonth: true,
                entriesByDay.GetValueOrDefault(offset, Array.Empty<CalendarEntry>())))
            .ToArray();
        return new WeekCalendarDto(WeekStart, WeekStart.AddDays(6), days);
    }

    private static CalendarEntry CreateEntry(
        string title,
        DateOnly displayDate,
        bool isPlannedOnDate = false,
        bool isDeadlineOnDate = false)
    {
        var task = new TaskDto(
            Guid.NewGuid(),
            title,
            Guid.Empty,
            TaskPriorityCode.Normal,
            WorkflowStatusCode.Pending,
            isPlannedOnDate ? displayDate : null,
            isPlannedOnDate ? new TimeOnly(10, 0) : null,
            null,
            isDeadlineOnDate
                ? new DeadlineDto(
                    displayDate,
                    new TimeOnly(18, 0),
                    "UTC",
                    new DateTimeOffset(displayDate.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero))
                : null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            NowUtc.AddDays(-1),
            NowUtc,
            null,
            1);
        return new CalendarEntry(
            task,
            displayDate,
            isPlannedOnDate,
            isDeadlineOnDate,
            DisplayStatusCode.NotStarted,
            isDeadlineOnDate ? DeadlineUrgencyCode.MoreThanThreeDays : DeadlineUrgencyCode.None);
    }

    private static ApplicationResult<T> Success<T>(T value) => ApplicationResult<T>.Success(value);

    private static ApplicationResult<T> Failure<T>(ApplicationError error) => ApplicationResult<T>.Failure(error);

    private sealed class FakeTaskUseCases : ITaskUseCases
    {
        public ApplicationResult<WeekCalendarDto> WeekCalendar { get; set; } = Success(CreateWeek());

        public List<DateOnly> RequestedWeekStarts { get; } = new();

        public int WeekCalendarCalls { get; private set; }

        public Task<ApplicationResult<TaskDto>> CreateAsync(
            CreateTaskCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<TaskDto>());

        public Task<ApplicationResult<TaskDto>> UpdateAsync(
            UpdateTaskCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<TaskDto>());

        public Task<ApplicationResult<TaskDto>> CompleteAsync(
            ChangeTaskStateCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<TaskDto>());

        public Task<ApplicationResult<TaskDto>> CancelCompletionAsync(
            ChangeTaskStateCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<TaskDto>());

        public Task<ApplicationResult<TaskDto>> StartProcessingAsync(
            ChangeTaskStateCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<TaskDto>());

        public Task<ApplicationResult<DeletedTaskDto>> DeleteAsync(
            DeleteTaskCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<DeletedTaskDto>());

        public Task<ApplicationResult<TaskDto>> GetAsync(
            GetTaskQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<TaskDto>());

        public Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetPlannedByDateAsync(
            GetTasksByPlannedDateQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<IReadOnlyList<TaskDto>>());

        public Task<ApplicationResult<IReadOnlyList<TaskDto>>> GetUpcomingDeadlinesAsync(
            GetUpcomingDeadlinesQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<IReadOnlyList<TaskDto>>());

        public Task<ApplicationResult<IReadOnlyList<CategoryOptionDto>>> GetCategoryOptionsAsync(
            GetCategoryOptionsQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Success<IReadOnlyList<CategoryOptionDto>>(new[]
            {
                new CategoryOptionDto(Guid.Empty, "其他", "#667085", 0)
            }));
        }

        public Task<ApplicationResult<CalendarDayDto>> GetCalendarDateAsync(
            GetCalendarDateQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<CalendarDayDto>());

        public Task<ApplicationResult<TodayPendingDto>> GetTodayPendingAsync(
            GetTodayPendingQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<TodayPendingDto>());

        public Task<ApplicationResult<WeekCalendarDto>> GetWeekCalendarAsync(
            GetWeekCalendarQuery query,
            CancellationToken cancellationToken = default)
        {
            WeekCalendarCalls++;
            RequestedWeekStarts.Add(query.Date);
            return Task.FromResult(WeekCalendar);
        }

        public Task<ApplicationResult<MonthCalendarDto>> GetMonthCalendarAsync(
            GetMonthCalendarQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<MonthCalendarDto>());

        public Task<ApplicationResult<DeadlineQueryResult>> GetDeadlinesAsync(
            GetDeadlinesQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<DeadlineQueryResult>());

        public Task<ApplicationResult<PagedResult<TaskDto>>> SearchAsync(
            SearchTasksQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<PagedResult<TaskDto>>());

        private static ApplicationResult<T> Unsupported<T>()
        {
            return Failure<T>(new ApplicationError(
                ApplicationErrorKind.Unexpected,
                "Test.Unsupported",
                "测试未配置此调用。"));
        }
    }

    private sealed class FakeDatabaseInitialization : IDatabaseInitialization
    {
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess => true;

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task InvokeAsync(Func<Task> action) => action();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
