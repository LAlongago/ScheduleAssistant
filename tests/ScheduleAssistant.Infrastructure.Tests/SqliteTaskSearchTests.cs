using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class SqliteTaskSearchTests
{
    [Fact]
    public async Task SearchAsync_WhenFiltersAndPagesAreApplied_ShouldCountAndOrderTheSameStableSet()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var matchingCategory = PersistenceTestData.CreateCategory();
        var otherCategory = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(matchingCategory);
        await database.Categories.AddAsync(otherCategory);
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var deadline = SqliteTaskQueryTestData.CreateDeadline(
            new DateOnly(2025, 12, 31),
            new TimeOnly(23, 0),
            now.AddHours(-1));
        var first = SqliteTaskQueryTestData.CreateTask(
            matchingCategory.Id,
            "page first",
            PersistenceTestData.CreatedAtUtc,
            priority: TaskPriority.UrgentAndImportant,
            plannedDate: new DateOnly(2026, 1, 3),
            deadline: deadline);
        var second = SqliteTaskQueryTestData.CreateTask(
            matchingCategory.Id,
            "page second",
            PersistenceTestData.CreatedAtUtc.AddMinutes(1),
            priority: TaskPriority.UrgentAndImportant,
            plannedDate: new DateOnly(2026, 1, 3),
            deadline: deadline);
        var third = SqliteTaskQueryTestData.CreateTask(
            matchingCategory.Id,
            "page third",
            PersistenceTestData.CreatedAtUtc.AddMinutes(2),
            priority: TaskPriority.UrgentAndImportant,
            plannedDate: new DateOnly(2026, 1, 3),
            deadline: deadline);
        var wrongPriority = SqliteTaskQueryTestData.CreateTask(
            matchingCategory.Id,
            "page wrong priority",
            PersistenceTestData.CreatedAtUtc.AddMinutes(3),
            priority: TaskPriority.Important,
            plannedDate: new DateOnly(2026, 1, 3),
            deadline: deadline);
        var wrongCategory = SqliteTaskQueryTestData.CreateTask(
            otherCategory.Id,
            "page wrong category",
            PersistenceTestData.CreatedAtUtc.AddMinutes(4),
            priority: TaskPriority.UrgentAndImportant,
            plannedDate: new DateOnly(2026, 1, 3),
            deadline: deadline);
        var completed = SqliteTaskQueryTestData.CreateTask(
            matchingCategory.Id,
            "page completed",
            PersistenceTestData.CreatedAtUtc.AddMinutes(5),
            priority: TaskPriority.UrgentAndImportant,
            workflowStatus: WorkflowStatus.Completed,
            plannedDate: new DateOnly(2026, 1, 3),
            deadline: deadline);
        foreach (var task in new[] { first, second, third, wrongPriority, wrongCategory, completed })
        {
            await database.Tasks.AddAsync(task);
        }

        var filter = new TaskSearchFilter(
            "page",
            matchingCategory.Id,
            TaskPriority.UrgentAndImportant,
            WorkflowStatus.Pending,
            IsOverdue: true,
            now);
        var pageOne = await database.Tasks.SearchAsync(filter, offset: 0, limit: 2);
        var pageTwo = await database.Tasks.SearchAsync(filter, offset: 2, limit: 2);
        var repeatedPageOne = await database.Tasks.SearchAsync(filter, offset: 0, limit: 2);
        var total = await database.Tasks.CountSearchAsync(filter);

        Assert.Equal(3, total);
        Assert.Equal(new[] { first.Id, second.Id }, pageOne.Select(task => task.Id));
        Assert.Equal(new[] { third.Id }, pageTwo.Select(task => task.Id));
        Assert.Equal(pageOne.Select(task => task.Id), repeatedPageOne.Select(task => task.Id));
    }

    [Fact]
    public async Task SearchAsync_WhenKeywordIsEmpty_ShouldFindTasksWithoutDatesOrDeadlines()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var task = SqliteTaskQueryTestData.CreateTask(
            category.Id,
            "无日期任务",
            PersistenceTestData.CreatedAtUtc);
        await database.Tasks.AddAsync(task);

        var filter = new TaskSearchFilter(
            string.Empty,
            null,
            null,
            null,
            null,
            PersistenceTestData.CreatedAtUtc);
        var result = await database.Tasks.SearchAsync(filter, offset: 0, limit: 10);
        var total = await database.Tasks.CountSearchAsync(filter);

        Assert.Equal(1, total);
        Assert.Single(result, item => item.Id == task.Id);
    }
}
