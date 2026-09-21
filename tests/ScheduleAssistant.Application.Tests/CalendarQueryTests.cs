using ScheduleAssistant.Application.Calendar;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Application.Tasks;
using Xunit;

namespace ScheduleAssistant.Application.Tests;

public sealed class CalendarQueryTests
{
    [Fact]
    public async Task GetCalendarDateAsync_WhenTaskIsPlannedAndDueOnSameDate_ShouldMergeFlagsByTaskId()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var date = new DateOnly(2026, 1, 2);
        var first = CreateTask(category.Id, "同名", plannedDate: date, deadline: CreateDeadline(date));
        var second = CreateTask(category.Id, "同名", plannedDate: date);
        Add(context, first);
        Add(context, second);

        var result = await context.UseCases.GetCalendarDateAsync(new GetCalendarDateQuery(date));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Entries.Count);
        var merged = Assert.Single(result.Value.Entries, entry => entry.Task.Id == first.Id);
        Assert.True(merged.IsPlannedOnDate);
        Assert.True(merged.IsDeadlineOnDate);
        Assert.Equal(2, result.Value.Entries.Count(entry => entry.Task.Title == "同名"));
    }

    [Fact]
    public async Task GetCalendarDateAsync_WhenDatesDifferOrDeadlineIsOnlyField_ShouldKeepSeparateEntries()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var plannedDate = new DateOnly(2026, 1, 2);
        var deadlineDate = new DateOnly(2026, 1, 3);
        var split = CreateTask(category.Id, "跨日", plannedDate, CreateDeadline(deadlineDate));
        var deadlineOnly = CreateTask(category.Id, "仅截止", deadline: CreateDeadline(new DateOnly(2026, 1, 4)));
        Add(context, split);
        Add(context, deadlineOnly);

        var plannedResult = await context.UseCases.GetCalendarDateAsync(new GetCalendarDateQuery(plannedDate));
        var deadlineResult = await context.UseCases.GetCalendarDateAsync(new GetCalendarDateQuery(deadlineDate));
        var deadlineOnlyResult = await context.UseCases.GetCalendarDateAsync(
            new GetCalendarDateQuery(new DateOnly(2026, 1, 4)));

        Assert.Single(plannedResult.Value!.Entries, entry => entry.Task.Id == split.Id && entry.IsPlannedOnDate && !entry.IsDeadlineOnDate);
        Assert.Single(deadlineResult.Value!.Entries, entry => entry.Task.Id == split.Id && !entry.IsPlannedOnDate && entry.IsDeadlineOnDate);
        Assert.Single(deadlineOnlyResult.Value!.Entries, entry => entry.Task.Id == deadlineOnly.Id && entry.IsDeadlineOnDate);
    }

    [Fact]
    public async Task GetTodayPendingAsync_WhenPlanIsHistorical_ShouldAggregateItOnlyInTodayPending()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var historical = CreateTask(category.Id, "历史计划", plannedDate: new DateOnly(2025, 12, 31));
        Add(context, historical);

        var dateResult = await context.UseCases.GetCalendarDateAsync(
            new GetCalendarDateQuery(new DateOnly(2026, 1, 1)));
        var todayResult = await context.UseCases.GetTodayPendingAsync(new GetTodayPendingQuery());

        Assert.Empty(dateResult.Value!.Entries);
        var entry = Assert.Single(todayResult.Value!.Entries);
        Assert.Equal(historical.Id, entry.Task.Id);
        Assert.Equal(new DateOnly(2026, 1, 1), entry.DisplayDate);
        Assert.Equal(DisplayStatusCode.PlannedPast, entry.DisplayStatus);
        Assert.False(entry.IsPlannedOnDate);
    }

    [Fact]
    public async Task GetWeekCalendarAsync_WhenDateCrossesNewYear_ShouldReturnMondayThroughSunday()
    {
        await using var context = new TaskUseCaseTestContext();

        var result = await context.UseCases.GetWeekCalendarAsync(
            new GetWeekCalendarQuery(new DateOnly(2026, 1, 1)));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2025, 12, 29), result.Value!.WeekStart);
        Assert.Equal(new DateOnly(2026, 1, 4), result.Value.WeekEnd);
        Assert.Equal(7, result.Value.Days.Count);
        Assert.Equal(result.Value.WeekStart, result.Value.Days[0].Date);
        Assert.Equal(result.Value.WeekEnd, result.Value.Days[^1].Date);
    }

    [Fact]
    public async Task GetMonthCalendarAsync_WhenLeapYearMonthIsRequested_ShouldReturn42DaysAndAdjacentMonthFlags()
    {
        await using var context = new TaskUseCaseTestContext();

        var result = await context.UseCases.GetMonthCalendarAsync(
            new GetMonthCalendarQuery(new DateOnly(2024, 2, 15)));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2024, 2, 1), result.Value!.Month);
        Assert.Equal(new DateOnly(2024, 1, 29), result.Value.GridStart);
        Assert.Equal(new DateOnly(2024, 3, 10), result.Value.GridEnd);
        Assert.Equal(42, result.Value.Days.Count);
        Assert.False(result.Value.Days[0].IsInDisplayedMonth);
        Assert.Equal(new DateOnly(2024, 2, 29), result.Value.Days[31].Date);
        Assert.True(result.Value.Days[31].IsInDisplayedMonth);
        Assert.False(result.Value.Days[^1].IsInDisplayedMonth);
        Assert.All(result.Value.Days, day => Assert.NotNull(day.Entries));
    }

    [Fact]
    public async Task GetDeadlinesAsync_WhenAtDeadline_ShouldBeUpcomingAndExcludeOverdueCompletedTasks()
    {
        await using var context = new TaskUseCaseTestContext();
        var category = context.SeedCategory();
        var atNow = CreateTask(
            category.Id,
            "到点",
            deadline: CreateDeadline(new DateOnly(2026, 1, 1), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var overdue = CreateTask(
            category.Id,
            "逾期",
            deadline: CreateDeadline(new DateOnly(2025, 12, 31), new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero)));
        var completed = CreateTask(
            category.Id,
            "已完成",
            deadline: CreateDeadline(new DateOnly(2025, 12, 30), new DateTimeOffset(2025, 12, 30, 23, 59, 59, TimeSpan.Zero)));
        completed.Complete(new DateTimeOffset(2025, 12, 30, 12, 0, 0, TimeSpan.Zero));
        Add(context, atNow);
        Add(context, overdue);
        Add(context, completed);

        var result = await context.UseCases.GetDeadlinesAsync(new GetDeadlinesQuery());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Upcoming, entry => entry.Task.Id == atNow.Id);
        Assert.Single(result.Value.Overdue, entry => entry.Task.Id == overdue.Id);
        Assert.DoesNotContain(result.Value.Upcoming.Concat(result.Value.Overdue), entry => entry.Task.Id == completed.Id);
    }

    private static TaskItem CreateTask(
        Guid categoryId,
        string title,
        DateOnly? plannedDate = null,
        ZonedDeadline? deadline = null)
    {
        return TaskItem.Create(
            Guid.NewGuid(),
            title,
            categoryId,
            TaskPriority.Normal,
            new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero),
            plannedDate,
            deadline: deadline);
    }

    private static ZonedDeadline CreateDeadline(
        DateOnly localDate,
        DateTimeOffset? utc = null)
    {
        return ZonedDeadline.CreateResolvedUtc(
            localDate,
            new TimeOnly(12, 0),
            "China Standard Time",
            utc ?? new DateTimeOffset(localDate.ToDateTime(new TimeOnly(4, 0)), TimeSpan.Zero));
    }

    private static void Add(TaskUseCaseTestContext context, TaskItem task)
    {
        context.Store.Tasks[task.Id] = TaskUseCaseTestContext.Clone(task);
    }
}
