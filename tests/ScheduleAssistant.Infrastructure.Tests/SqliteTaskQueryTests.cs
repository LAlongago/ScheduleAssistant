using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class SqliteTaskQueryTests
{
    [Fact]
    public async Task GetByRangeAsync_WhenDeadlineLocalDateDiffersFromUtcDate_ShouldUseSavedLocalDate()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var localDate = new DateOnly(2026, 1, 2);
        var deadline = SqliteTaskQueryTestData.CreateDeadline(
            localDate,
            new TimeOnly(0, 30),
            new DateTimeOffset(2026, 1, 1, 16, 30, 0, TimeSpan.Zero));
        var task = SqliteTaskQueryTestData.CreateTask(
            category.Id,
            "本地截止日期",
            PersistenceTestData.CreatedAtUtc,
            deadline: deadline);
        await database.Tasks.AddAsync(task);

        var localDateResults = await database.Tasks.GetByRangeAsync(localDate, localDate);
        var utcDateResults = await database.Tasks.GetByRangeAsync(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 1));

        Assert.Contains(localDateResults, item => item.Id == task.Id);
        Assert.DoesNotContain(utcDateResults, item => item.Id == task.Id);
    }

    [Fact]
    public async Task GetDeadlinesAsync_WhenAtNowAndOverdue_ShouldApplyBoundaryAndCompletionFilters()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var atNow = SqliteTaskQueryTestData.CreateTask(
            category.Id,
            "到点",
            PersistenceTestData.CreatedAtUtc,
            deadline: SqliteTaskQueryTestData.CreateDeadline(
                new DateOnly(2026, 1, 1),
                new TimeOnly(8, 0),
                now));
        var withinWindow = SqliteTaskQueryTestData.CreateTask(
            category.Id,
            "窗口内",
            PersistenceTestData.CreatedAtUtc.AddMinutes(1),
            deadline: SqliteTaskQueryTestData.CreateDeadline(
                new DateOnly(2026, 1, 1),
                new TimeOnly(20, 0),
                now.AddHours(20)));
        var outsideWindow = SqliteTaskQueryTestData.CreateTask(
            category.Id,
            "窗口外",
            PersistenceTestData.CreatedAtUtc.AddMinutes(2),
            deadline: SqliteTaskQueryTestData.CreateDeadline(
                new DateOnly(2026, 1, 2),
                new TimeOnly(8, 0),
                now.AddHours(25)));
        var overdue = SqliteTaskQueryTestData.CreateTask(
            category.Id,
            "逾期",
            PersistenceTestData.CreatedAtUtc.AddMinutes(3),
            deadline: SqliteTaskQueryTestData.CreateDeadline(
                new DateOnly(2025, 12, 31),
                new TimeOnly(23, 0),
                now.AddHours(-1)));
        var completed = SqliteTaskQueryTestData.CreateTask(
            category.Id,
            "已完成",
            PersistenceTestData.CreatedAtUtc.AddMinutes(4),
            workflowStatus: WorkflowStatus.Completed,
            deadline: SqliteTaskQueryTestData.CreateDeadline(
                new DateOnly(2025, 12, 30),
                new TimeOnly(23, 0),
                now.AddHours(-2)));
        await database.Tasks.AddAsync(atNow);
        await database.Tasks.AddAsync(withinWindow);
        await database.Tasks.AddAsync(outsideWindow);
        await database.Tasks.AddAsync(overdue);
        await database.Tasks.AddAsync(completed);

        var window = await database.Tasks.GetDeadlinesAsync(now, now.AddHours(24), includeOverdue: false);
        var all = await database.Tasks.GetDeadlinesAsync(now, untilUtc: null, includeOverdue: true);

        Assert.Equal(new[] { atNow.Id, withinWindow.Id }, window.Select(item => item.Id));
        Assert.Equal(
            new[] { overdue.Id, atNow.Id, withinWindow.Id, outsideWindow.Id },
            all.Select(item => item.Id));
        Assert.DoesNotContain(all, item => item.Id == completed.Id);
    }

    [Fact]
    public async Task SearchAsync_WhenKeywordMatchesEachTextFieldOrLiteralWildcards_ShouldReturnOnlyLiteralMatches()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var created = PersistenceTestData.CreatedAtUtc;
        var cases = new Dictionary<string, Guid>();
        var title = SqliteTaskQueryTestData.CreateTask(category.Id, "标题字段 needle-title", created);
        var location = SqliteTaskQueryTestData.CreateTask(category.Id, "地点字段", created.AddMinutes(1), location: "needle-location");
        var description = SqliteTaskQueryTestData.CreateTask(category.Id, "具体事务字段", created.AddMinutes(2), description: "needle-description");
        var materials = SqliteTaskQueryTestData.CreateTask(category.Id, "材料字段", created.AddMinutes(3), materials: "needle-materials");
        var notes = SqliteTaskQueryTestData.CreateTask(category.Id, "备注字段", created.AddMinutes(4), notes: "needle-notes");
        var literal = SqliteTaskQueryTestData.CreateTask(category.Id, "100%_literal", created.AddMinutes(5));
        var wildcardDecoy = SqliteTaskQueryTestData.CreateTask(category.Id, "100XYZliteral", created.AddMinutes(6));
        foreach (var task in new[] { title, location, description, materials, notes, literal, wildcardDecoy })
        {
            await database.Tasks.AddAsync(task);
        }

        cases["needle-title"] = title.Id;
        cases["needle-location"] = location.Id;
        cases["needle-description"] = description.Id;
        cases["needle-materials"] = materials.Id;
        cases["needle-notes"] = notes.Id;
        foreach (var pair in cases)
        {
            var filter = new TaskSearchFilter(
                pair.Key,
                CategoryId: null,
                Priority: null,
                WorkflowStatus: null,
                IsOverdue: null,
                PersistenceTestData.CreatedAtUtc);
            var found = await database.Tasks.SearchAsync(filter, offset: 0, limit: 20);
            Assert.Single(found, task => task.Id == pair.Value);
        }

        var literalFilter = new TaskSearchFilter(
            "100%_literal",
            null,
            null,
            null,
            null,
            PersistenceTestData.CreatedAtUtc);
        var literalResults = await database.Tasks.SearchAsync(literalFilter, 0, 20);

        Assert.Equal(new[] { literal.Id }, literalResults.Select(task => task.Id));
    }
}
