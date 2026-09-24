using System.Globalization;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.Composition;
using ScheduleAssistant.Presentation.Controls;
using ScheduleAssistant.Presentation.ViewModels;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class MonthPageViewModelTests
{
    private static readonly DateOnly Today = new(2026, 9, 15);
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly string[] Weekdays = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];
    private static readonly string[] FirstThreeTitles = ["事项 1", "事项 2", "事项 3"];
    private static readonly string[] HighValueTitles = ["逾期", "今日 Deadline", "紧急事项"];
    private static readonly string[] DetailTitles = ["完整事项 1", "完整事项 2", "完整事项 3", "完整事项 4"];

    [Fact]
    public async Task LoadAsync_WhenMonthQueryReturnsFixedGrid_ShouldExpose42CellsAndAdjacentMonthFlags()
    {
        var queries = new FakeTaskQueries
        {
            MonthCalendar = CreateMonthCalendar(Today, new Dictionary<DateOnly, IReadOnlyList<CalendarEntry>>())
        };
        using var page = CreatePage(queries, new FakeTaskUseCases());

        await page.LoadAsync();

        Assert.Equal(42, page.Days.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), page.Days[0].Date);
        Assert.False(page.Days[0].IsInDisplayedMonth);
        Assert.Equal(new DateOnly(2026, 9, 1), page.Days[1].Date);
        Assert.True(page.Days[1].IsInDisplayedMonth);
        Assert.False(page.Days[^1].IsInDisplayedMonth);
        Assert.Equal(Weekdays, page.WeekdayLabels);
        Assert.Equal(PageContentState.Empty, page.ContentState);
    }

    [Fact]
    public async Task LoadAsync_WhenDateHasMoreThanThreeEntries_ShouldExposeThreeAndPlusTwo()
    {
        var date = Today;
        var entries = Enumerable.Range(1, 5)
            .Select(index => CreateEntry(date, $"事项 {index}"))
            .ToArray();
        var queries = new FakeTaskQueries
        {
            MonthCalendar = CreateMonthCalendar(
                Today,
                new Dictionary<DateOnly, IReadOnlyList<CalendarEntry>> { [date] = entries })
        };
        using var page = CreatePage(queries, new FakeTaskUseCases());

        await page.LoadAsync();

        var cell = Assert.Single(page.Days, day => day.Date == date);
        Assert.Equal(5, cell.Entries.Count);
        Assert.Equal(3, cell.VisibleEntries.Count);
        Assert.Equal(2, cell.AdditionalEntryCount);
        Assert.True(cell.HasAdditionalEntries);
        Assert.Equal(FirstThreeTitles, cell.VisibleEntries.Select(card => card.Title));
    }

    [Fact]
    public async Task LoadAsync_WhenApplicationReturnsHighValueOrder_ShouldKeepStableOrderBeforeTruncating()
    {
        var date = Today;
        var entries = new[]
        {
            CreateEntry(date, "逾期", displayStatus: DisplayStatusCode.Overdue),
            CreateEntry(date, "今日 Deadline", isPlanned: false, isDeadline: true),
            CreateEntry(date, "紧急事项", priority: TaskPriorityCode.UrgentAndImportant),
            CreateEntry(date, "带时间计划", plannedStart: new TimeOnly(14, 0)),
            CreateEntry(date, "其他事项")
        };
        var queries = new FakeTaskQueries
        {
            MonthCalendar = CreateMonthCalendar(
                Today,
                new Dictionary<DateOnly, IReadOnlyList<CalendarEntry>> { [date] = entries })
        };
        using var page = CreatePage(queries, new FakeTaskUseCases());

        await page.LoadAsync();

        var cell = Assert.Single(page.Days, day => day.Date == date);
        Assert.Equal(
            HighValueTitles,
            cell.VisibleEntries.Select(card => card.Title));
        Assert.Equal(2, cell.AdditionalEntryCount);
    }

    [Fact]
    public async Task MonthCommands_WhenNavigating_ShouldLoadPreviousNextAndCurrentMonth()
    {
        var queries = new FakeTaskQueries();
        using var page = CreatePage(queries, new FakeTaskUseCases());

        await page.LoadAsync();
        await page.PreviousMonthCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 8, 1), page.DisplayedMonth);
        Assert.Equal(new DateOnly(2026, 8, 1), queries.MonthQueries[^1].Date);

        await page.NextMonthCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 9, 1), page.DisplayedMonth);

        await page.NextMonthCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 10, 1), page.DisplayedMonth);
        await page.CurrentMonthCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 9, 1), page.DisplayedMonth);
    }

    [Fact]
    public async Task OpenDateDetailsCommand_WhenDateSelected_ShouldLoadCompleteDateEntries()
    {
        var date = Today;
        var gridEntry = CreateEntry(date, "网格摘要");
        var completeEntries = Enumerable.Range(1, 4)
            .Select(index => CreateEntry(date, $"完整事项 {index}"))
            .ToArray();
        var queries = new FakeTaskQueries
        {
            MonthCalendar = CreateMonthCalendar(
                Today,
                new Dictionary<DateOnly, IReadOnlyList<CalendarEntry>> { [date] = new[] { gridEntry } }),
            DateCalendars =
            {
                [date] = new CalendarDayDto(date, true, completeEntries)
            }
        };
        using var page = CreatePage(queries, new FakeTaskUseCases());

        await page.LoadAsync();
        await page.OpenDateDetailsCommand.ExecuteAsync(date);

        Assert.Equal(1, queries.DateQueryCalls);
        Assert.True(page.IsDateDetailsOpen);
        Assert.Equal(date, page.SelectedDate);
        Assert.Equal(PageContentState.Ready, page.DateDetailsState);
        Assert.Equal(4, page.DateDetails.Count);
        Assert.All(page.DateDetails, card => Assert.Equal(TaskCardMode.Compact, card.Mode));
        Assert.Equal(
            DetailTitles,
            page.DateDetails.Select(card => card.Title));
    }

    [Fact]
    public async Task ApplicationEvent_WhenAffectedDateIsInCurrentGrid_ShouldRefreshMonthGrid()
    {
        var queries = new FakeTaskQueries();
        var eventBus = new InProcessEventBus();
        using var page = CreatePage(queries, new FakeTaskUseCases(), eventBus);

        await page.LoadAsync();
        Assert.Equal(1, queries.MonthQueryCalls);

        await eventBus.PublishAsync(new TaskUpdated(
            Guid.NewGuid(),
            2,
            new[] { Today },
            DeadlineChanged: true));

        Assert.Equal(2, queries.MonthQueryCalls);
    }

    [Fact]
    public async Task ApplicationEvent_WhenAffectedDateIsOutsideCurrentGrid_ShouldNotRefreshMonthGrid()
    {
        var queries = new FakeTaskQueries();
        var eventBus = new InProcessEventBus();
        using var page = CreatePage(queries, new FakeTaskUseCases(), eventBus);

        await page.LoadAsync();
        await eventBus.PublishAsync(new TaskUpdated(
            Guid.NewGuid(),
            2,
            new[] { new DateOnly(2030, 1, 1) },
            DeadlineChanged: false));

        Assert.Equal(1, queries.MonthQueryCalls);
    }

    private static MonthPageViewModel CreatePage(
        FakeTaskQueries queries,
        FakeTaskUseCases useCases,
        InProcessEventBus? eventBus = null)
    {
        return new MonthPageViewModel(
            queries,
            useCases,
            new FakeTaskCardMapper(),
            new FakeDatabaseInitialization(),
            eventBus ?? new InProcessEventBus(),
            new InlineDispatcher(),
            new FixedTimeProvider());
    }

    private static MonthCalendarDto CreateMonthCalendar(
        DateOnly date,
        Dictionary<DateOnly, IReadOnlyList<CalendarEntry>> entriesByDate)
    {
        var month = new DateOnly(date.Year, date.Month, 1);
        var gridStart = StartOfWeek(month);
        var days = Enumerable.Range(0, 42)
            .Select(offset =>
            {
                var current = gridStart.AddDays(offset);
                return new CalendarDayDto(
                    current,
                    current.Year == month.Year && current.Month == month.Month,
                    entriesByDate.TryGetValue(current, out var entries)
                        ? entries
                        : Array.Empty<CalendarEntry>());
            })
            .ToArray();
        return new MonthCalendarDto(month, gridStart, gridStart.AddDays(41), days);
    }

    private static CalendarEntry CreateEntry(
        DateOnly date,
        string title,
        bool isPlanned = true,
        bool isDeadline = false,
        TaskPriorityCode priority = TaskPriorityCode.Normal,
        DisplayStatusCode displayStatus = DisplayStatusCode.NotStarted,
        TimeOnly? plannedStart = null)
    {
        var deadline = isDeadline
            ? new DeadlineDto(
                date,
                new TimeOnly(18, 0),
                "UTC",
                new DateTimeOffset(date.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero))
            : null;
        var task = new TaskDto(
            Guid.NewGuid(),
            title,
            CategoryId,
            priority,
            WorkflowStatusCode.Pending,
            isPlanned ? date : null,
            plannedStart,
            plannedStart.HasValue ? plannedStart.Value.AddHours(1) : null,
            deadline,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            1);
        return new CalendarEntry(
            task,
            date,
            isPlanned,
            isDeadline,
            displayStatus,
            isDeadline ? DeadlineUrgencyCode.LessThanOneDay : DeadlineUrgencyCode.None);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysFromMonday);
    }

    private class FakeTaskQueries : ITaskQueries
    {
        public MonthCalendarDto MonthCalendar { get; set; } = CreateMonthCalendar(
            Today,
            new Dictionary<DateOnly, IReadOnlyList<CalendarEntry>>());

        public Dictionary<DateOnly, CalendarDayDto> DateCalendars { get; } = new();

        public List<GetMonthCalendarQuery> MonthQueries { get; } = new();

        public int MonthQueryCalls { get; private set; }

        public int DateQueryCalls { get; private set; }

        public Task<ApplicationResult<CalendarDayDto>> GetCalendarDateAsync(
            GetCalendarDateQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateQueryCalls++;
            var value = DateCalendars.GetValueOrDefault(
                query.Date,
                new CalendarDayDto(query.Date, true, Array.Empty<CalendarEntry>()));
            return Task.FromResult(ApplicationResult<CalendarDayDto>.Success(value));
        }

        public Task<ApplicationResult<TodayPendingDto>> GetTodayPendingAsync(
            GetTodayPendingQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<TodayPendingDto>());

        public Task<ApplicationResult<WeekCalendarDto>> GetWeekCalendarAsync(
            GetWeekCalendarQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<WeekCalendarDto>());

        public Task<ApplicationResult<MonthCalendarDto>> GetMonthCalendarAsync(
            GetMonthCalendarQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MonthQueryCalls++;
            MonthQueries.Add(query);
            return Task.FromResult(ApplicationResult<MonthCalendarDto>.Success(MonthCalendar));
        }

        public Task<ApplicationResult<DeadlineQueryResult>> GetDeadlinesAsync(
            GetDeadlinesQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<DeadlineQueryResult>());

        public Task<ApplicationResult<PagedResult<TaskDto>>> SearchAsync(
            SearchTasksQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Unsupported<PagedResult<TaskDto>>());
    }

    private sealed class FakeTaskUseCases : FakeTaskQueries, ITaskUseCases
    {
        public IReadOnlyList<CategoryOptionDto> Categories { get; } = new[]
        {
            new CategoryOptionDto(CategoryId, "其他", "#667085", 0)
        };

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
            => Task.FromResult(ApplicationResult<IReadOnlyList<CategoryOptionDto>>.Success(Categories));
    }

    private sealed class FakeTaskCardMapper : ITaskCardMapper
    {
        public TaskCardViewModel Map(
            CalendarEntry entry,
            IReadOnlyDictionary<Guid, CategoryOptionDto> categories,
            TaskCardMode mode = TaskCardMode.Standard)
        {
            return new TaskCardViewModel(
                entry.Task.Title,
                "其他",
                entry.Task.PlannedStart?.ToString("HH\\:mm", CultureInfo.InvariantCulture) ?? "全天",
                entry.Task.Deadline is null ? "无 Deadline" : "有 Deadline",
                entry.Task.Priority.ToString(),
                string.Empty,
                entry.DisplayStatus.ToString(),
                CategoryAccent.Other,
                mode,
                entry.Task.WorkflowStatus == WorkflowStatusCode.Completed);
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
        public override DateTimeOffset GetUtcNow()
            => new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static ApplicationResult<T> Unsupported<T>()
    {
        return ApplicationResult<T>.Failure(
            new ApplicationError(ApplicationErrorKind.Unexpected, "Test.Unsupported", "测试未配置此调用。"));
    }
}
