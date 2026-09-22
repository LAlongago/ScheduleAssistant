using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Recurrence;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class RecurrenceMaterializationTests
{
    [Fact]
    public async Task MaterializeAsync_WhenMonthlyDay31AndExclusionExist_ShouldClampAndRemainIdempotent()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var series = RecurrenceSeries.Create(
            Guid.NewGuid(),
            "月末任务",
            category.Id,
            TaskPriority.Normal,
            RecurrenceRule.CreateMonthly(new DateOnly(2024, 1, 1), 31, "UTC"),
            PersistenceTestData.CreatedAtUtc);
        await database.Series.AddAsync(series);
        await database.Exclusions.AddAsync(new RecurrenceExclusion(series.Id, new DateOnly(2024, 2, 29)));

        var materializer = CreateMaterializer(database);
        await materializer.MaterializeAsync(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));
        await materializer.MaterializeAsync(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));

        var tasks = await database.Tasks.GetByRangeAsync(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));
        Assert.Equal(new[] { new DateOnly(2024, 1, 31), new DateOnly(2024, 3, 31) }, tasks.Select(task => task.OccurrenceDate!.Date));
        Assert.All(tasks, task =>
        {
            Assert.Equal(series.Id, task.SeriesId);
            Assert.Null(task.Deadline);
        });
        Assert.Empty(await database.Reminders.GetByTaskIdAsync(tasks[0].Id));
    }

    [Fact]
    public async Task MaterializeAsync_WhenTwoProcessesMaterializeSameRange_ShouldUseTheSeriesDateUniqueKey()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var series = RecurrenceSeries.Create(
            Guid.NewGuid(),
            "并发任务",
            category.Id,
            TaskPriority.Normal,
            RecurrenceRule.CreateDaily(new DateOnly(2026, 1, 1), "UTC", new DateOnly(2026, 1, 10)),
            PersistenceTestData.CreatedAtUtc);
        await database.Series.AddAsync(series);

        var first = CreateMaterializer(database);
        var second = CreateMaterializer(database);
        await Task.WhenAll(
            first.MaterializeAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10)),
            second.MaterializeAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10)));

        var tasks = await database.Tasks.GetByRangeAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        Assert.Equal(10, tasks.Count);
        Assert.Equal(10, tasks.Select(task => task.OccurrenceDate!.Date).Distinct().Count());
    }

    [Fact]
    public async Task MaterializeAsync_WhenWeeklyAndYearlyRulesCrossBoundaries_ShouldUseLocalDateRules()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var weekly = RecurrenceSeries.Create(
            Guid.NewGuid(),
            "跨年周任务",
            category.Id,
            TaskPriority.Normal,
            RecurrenceRule.CreateWeekly(
                new DateOnly(2025, 12, 1),
                RecurrenceWeekdayMask.Monday | RecurrenceWeekdayMask.Friday,
                "UTC"),
            PersistenceTestData.CreatedAtUtc);
        var yearly = RecurrenceSeries.Create(
            Guid.NewGuid(),
            "闰年任务",
            category.Id,
            TaskPriority.Normal,
            RecurrenceRule.CreateYearly(new DateOnly(2024, 1, 1), 2, 29, "UTC"),
            PersistenceTestData.CreatedAtUtc);
        await database.Series.AddAsync(weekly);
        await database.Series.AddAsync(yearly);

        await CreateMaterializer(database).MaterializeAsync(
            new DateOnly(2024, 2, 29),
            new DateOnly(2026, 3, 1));

        var weeklyDates = (await database.Tasks.GetByRangeAsync(
                new DateOnly(2025, 12, 29),
                new DateOnly(2026, 1, 5)))
            .Where(task => task.SeriesId == weekly.Id)
            .Select(task => task.OccurrenceDate!.Date)
            .ToArray();
        var yearlyDates = (await database.Tasks.GetByRangeAsync(
                new DateOnly(2024, 2, 29),
                new DateOnly(2026, 3, 1)))
            .Where(task => task.SeriesId == yearly.Id)
            .Select(task => task.OccurrenceDate!.Date)
            .ToArray();

        Assert.Equal(
            new[] { new DateOnly(2025, 12, 29), new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 5) },
            weeklyDates);
        Assert.Equal(
            new[] { new DateOnly(2024, 2, 29), new DateOnly(2025, 2, 28), new DateOnly(2026, 2, 28) },
            yearlyDates);
    }

    [Fact]
    public async Task MaterializeAsync_WhenNoWindowIsSupplied_ShouldUseTodayMinus31ThroughPlus400()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var series = RecurrenceSeries.Create(
            Guid.NewGuid(),
            "默认窗口任务",
            category.Id,
            TaskPriority.Normal,
            RecurrenceRule.CreateDaily(new DateOnly(2026, 8, 1), "UTC"),
            PersistenceTestData.CreatedAtUtc);
        await database.Series.AddAsync(series);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero));

        await CreateMaterializer(database, clock).MaterializeAsync();

        var tasks = await database.Tasks.GetByRangeAsync(new DateOnly(2026, 8, 21), new DateOnly(2027, 10, 26));
        Assert.Equal(432, tasks.Count);
        Assert.Equal(new DateOnly(2026, 8, 21), tasks.Min(task => task.OccurrenceDate!.Date));
        Assert.Equal(new DateOnly(2027, 10, 26), tasks.Max(task => task.OccurrenceDate!.Date));
    }

    [Fact]
    public async Task DeleteInstanceAsync_WhenMaterializerRestarts_ShouldKeepTheExclusion()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero));
        var materializer = CreateMaterializer(database, clock);
        var useCases = CreateUseCases(database, materializer, clock);
        var seriesResult = await useCases.CreateAsync(new CreateRecurrenceSeriesCommand(
            DailyDraft(category.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3))));
        Assert.True(seriesResult.IsSuccess);

        var occurrence = Assert.Single(
            await database.Tasks.GetByRangeAsync(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2)));
        var deleted = await useCases.DeleteInstanceAsync(
            new DeleteRecurrenceInstanceCommand(occurrence.Id, occurrence.Version));

        Assert.True(deleted.IsSuccess);
        Assert.True(await database.Exclusions.ExistsAsync(seriesResult.Value!.Id, new DateOnly(2026, 1, 2)));
        await CreateMaterializer(database, clock)
            .MaterializeAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3));
        Assert.DoesNotContain(
            await database.Tasks.GetByRangeAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3)),
            task => task.OccurrenceDate!.Date == new DateOnly(2026, 1, 2));
    }

    [Fact]
    public async Task UpdateAsync_WhenApplyingFromDate_ShouldPreserveCompletedPastAndOverrideInstances()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero));
        var materializer = CreateMaterializer(database, clock);
        var useCases = CreateUseCases(database, materializer, clock);
        var taskUseCases = new TaskUseCases(
            database.Tasks,
            database.Categories,
            database.Reminders,
            database.TransactionFactory,
            timeProvider: clock);
        var seriesResult = await useCases.CreateAsync(new CreateRecurrenceSeriesCommand(
            DailyDraft(category.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5))));
        Assert.True(seriesResult.IsSuccess);

        var originalTasks = await database.Tasks.GetByRangeAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        var completed = Assert.Single(originalTasks, task => task.OccurrenceDate!.Date == new DateOnly(2026, 1, 1));
        var completedResult = await taskUseCases.CompleteAsync(new ChangeTaskStateCommand(completed.Id, completed.Version));
        Assert.True(completedResult.IsSuccess);
        var overridden = Assert.Single(originalTasks, task => task.OccurrenceDate!.Date == new DateOnly(2026, 1, 2));
        var overrideResult = await taskUseCases.UpdateAsync(new UpdateTaskCommand(
            overridden.Id,
            overridden.Version,
            new TaskDraft("单独覆盖", category.Id, PlannedDate: overridden.PlannedDate)));
        Assert.True(overrideResult.IsSuccess);
        Assert.True(overrideResult.Value!.IsOccurrenceOverride);

        var updated = await useCases.UpdateAsync(new UpdateRecurrenceSeriesCommand(
            seriesResult.Value!.Id,
            seriesResult.Value.Version,
            DailyDraft(category.Id, new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 5), "新系列标题"),
            ApplyFromDate: new DateOnly(2026, 1, 3)));

        Assert.True(updated.IsSuccess);
        var tasks = await database.Tasks.GetByRangeAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        Assert.Equal(5, tasks.Count);
        Assert.Equal(WorkflowStatus.Completed, Assert.Single(tasks, task => task.OccurrenceDate!.Date == new DateOnly(2026, 1, 1)).WorkflowStatus);
        Assert.Equal("单独覆盖", Assert.Single(tasks, task => task.OccurrenceDate!.Date == new DateOnly(2026, 1, 2)).Title);
        Assert.All(
            tasks.Where(task => task.OccurrenceDate!.Date >= new DateOnly(2026, 1, 3)),
            task => Assert.Equal("新系列标题", task.Title));
    }

    [Fact]
    public async Task DeleteFutureAsync_WhenCompletedFutureTaskExists_ShouldPreserveItAndBlockRegeneration()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero));
        var materializer = CreateMaterializer(database, clock);
        var useCases = CreateUseCases(database, materializer, clock);
        var taskUseCases = new TaskUseCases(
            database.Tasks,
            database.Categories,
            database.Reminders,
            database.TransactionFactory,
            timeProvider: clock);
        var created = await useCases.CreateAsync(new CreateRecurrenceSeriesCommand(
            DailyDraft(category.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5))));
        var completedFuture = Assert.Single(
            await database.Tasks.GetByRangeAsync(new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 4)));
        Assert.True((await taskUseCases.CompleteAsync(
            new ChangeTaskStateCommand(completedFuture.Id, completedFuture.Version))).IsSuccess);

        var deleted = await useCases.DeleteFutureAsync(new DeleteFutureRecurrenceCommand(
            created.Value!.Id,
            created.Value.Version,
            new DateOnly(2026, 1, 3)));

        Assert.True(deleted.IsSuccess);
        Assert.NotNull(await database.Tasks.GetByIdAsync(completedFuture.Id));
        Assert.Equal(2, deleted.Value!.DeletedTaskIds.Count);
        foreach (var taskId in deleted.Value.DeletedTaskIds)
        {
            Assert.Null(await database.Tasks.GetByIdAsync(taskId));
        }
        await CreateMaterializer(database, clock)
            .MaterializeAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var remaining = await database.Tasks.GetByRangeAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        Assert.DoesNotContain(remaining, task => task.OccurrenceDate!.Date is { } date && date >= new DateOnly(2026, 1, 3) && task.Id != completedFuture.Id);
    }

    [Fact]
    public async Task UpdateAsync_WhenInstanceRebuildFails_ShouldRollbackTheSeriesUpdate()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var series = RecurrenceSeries.Create(
            Guid.NewGuid(),
            "原始系列",
            category.Id,
            TaskPriority.Normal,
            RecurrenceRule.CreateDaily(new DateOnly(2026, 1, 1), "UTC", new DateOnly(2026, 1, 3)),
            PersistenceTestData.CreatedAtUtc);
        await database.Series.AddAsync(series);
        var failingTasks = new FailingRecurrenceTaskRepository();
        var useCases = new RecurrenceUseCases(
            database.Series,
            database.Exclusions,
            database.Tasks,
            failingTasks,
            database.Categories,
            database.TransactionFactory,
            timeProvider: new FixedTimeProvider(PersistenceTestData.CreatedAtUtc.AddDays(1)));

        var result = await useCases.UpdateAsync(new UpdateRecurrenceSeriesCommand(
            series.Id,
            series.Version,
            DailyDraft(category.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), "不应保存"),
            ApplyFromDate: new DateOnly(2026, 1, 2)));

        Assert.False(result.IsSuccess);
        var reloaded = await database.Series.GetByIdAsync(series.Id);
        Assert.Equal("原始系列", reloaded!.Title);
        Assert.Equal(series.Version, reloaded.Version);
    }

    private static RecurrenceMaterializer CreateMaterializer(
        PersistenceTestDatabase database,
        TimeProvider? timeProvider = null)
    {
        return new RecurrenceMaterializer(
            database.Series,
            database.Exclusions,
            (IRecurrenceTaskRepository)database.Tasks,
            database.TransactionFactory,
            timeProvider: timeProvider ?? new FixedTimeProvider(PersistenceTestData.CreatedAtUtc));
    }

    private static RecurrenceUseCases CreateUseCases(
        PersistenceTestDatabase database,
        ITransactionalRecurrenceMaterializer materializer,
        TimeProvider timeProvider)
    {
        return new RecurrenceUseCases(
            database.Series,
            database.Exclusions,
            database.Tasks,
            (IRecurrenceTaskRepository)database.Tasks,
            database.Categories,
            database.TransactionFactory,
            materializer,
            timeProvider: timeProvider);
    }

    private static RecurrenceSeriesDraft DailyDraft(
        Guid categoryId,
        DateOnly effectiveDate,
        DateOnly endDate,
        string title = "周期任务")
    {
        return new RecurrenceSeriesDraft(
            title,
            categoryId,
            TaskPriorityCode.Normal,
            new RecurrenceRuleDraft(
                RecurrenceFrequencyCode.Daily,
                effectiveDate,
                "UTC",
                EndDate: endDate));
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

    private sealed class FailingRecurrenceTaskRepository : IRecurrenceTaskRepository
    {
        public Task<PersistenceCommitResult<TaskItem>?> AddIfAbsentAsync(
            TaskItem item,
            IPersistenceTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Test materialization failure.");
        }

        public Task<IReadOnlyList<TaskItem>> DeleteUncompletedNonOverrideBySeriesFromDateAsync(
            Guid seriesId,
            DateOnly fromDate,
            IPersistenceTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Test rebuild failure.");
        }
    }
}
