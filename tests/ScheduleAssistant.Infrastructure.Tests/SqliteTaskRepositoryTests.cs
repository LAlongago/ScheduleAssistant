using ScheduleAssistant.Domain;
using ScheduleAssistant.Infrastructure.Persistence;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class SqliteTaskRepositoryTests
{
    [Fact]
    public async Task TaskRepository_WhenDatabaseIsReopened_ShouldRoundTripFieldsAndIncludeDeadlineOnlyRangeResults()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);

        var plannedDate = new DateOnly(2026, 2, 3);
        var deadline = PersistenceTestData.CreateDeadline(new DateOnly(2026, 2, 5));
        var task = PersistenceTestData.CreateTask(category.Id, plannedDate: plannedDate, deadline: deadline);
        await database.Tasks.AddAsync(task);

        var deadlineOnly = TaskItem.Create(
            Guid.NewGuid(),
            "仅截止时间",
            category.Id,
            TaskPriority.Normal,
            PersistenceTestData.CreatedAtUtc,
            deadline: deadline);
        await database.Tasks.AddAsync(deadlineOnly);

        var reopened = PersistenceTestDatabase.OpenExisting(database.RootDirectory);
        await reopened.Initializer.InitializeAsync();
        var reloaded = await reopened.Tasks.GetByIdAsync(task.Id);
        var planned = await reopened.Tasks.GetPlannedByDateAsync(plannedDate);
        var range = await reopened.Tasks.GetByRangeAsync(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5));
        var upcoming = await reopened.Tasks.GetUpcomingDeadlinesAsync(
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 2, 6, 0, 0, 0, TimeSpan.Zero));

        Assert.NotNull(reloaded);
        Assert.Equal(task.Id, reloaded!.Id);
        Assert.Equal(task.Title, reloaded.Title);
        Assert.Equal(task.Priority, reloaded.Priority);
        Assert.Equal(task.PlannedDate, reloaded.PlannedDate);
        Assert.Equal(task.PlannedStart, reloaded.PlannedStart);
        Assert.Equal(task.PlannedEnd, reloaded.PlannedEnd);
        Assert.Equal(task.Deadline!.LocalDate, reloaded.Deadline!.LocalDate);
        Assert.Equal(task.Deadline.LocalTime, reloaded.Deadline.LocalTime);
        Assert.Equal(task.Deadline.TimeZoneId, reloaded.Deadline.TimeZoneId);
        Assert.Equal(task.Deadline.Utc, reloaded.Deadline.Utc);
        Assert.Equal(task.Location, reloaded.Location);
        Assert.Equal(task.Description, reloaded.Description);
        Assert.Equal(task.Materials, reloaded.Materials);
        Assert.Equal(task.Notes, reloaded.Notes);
        Assert.Equal(task.CreatedAtUtc, reloaded.CreatedAtUtc);
        Assert.Equal(task.UpdatedAtUtc, reloaded.UpdatedAtUtc);
        Assert.Equal(task.Version, reloaded.Version);
        Assert.Single(planned);
        Assert.Contains(range, item => item.Id == task.Id);
        Assert.Contains(range, item => item.Id == deadlineOnly.Id);
        Assert.Equal(2, upcoming.Count);
    }

    [Fact]
    public async Task TaskRepository_WhenOldVersionIsUpdatedTwice_ShouldRejectSecondWriteWithoutOverwritingData()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var original = PersistenceTestData.CreateTask(category.Id);
        await database.Tasks.AddAsync(original);

        var firstCopy = await database.Tasks.GetByIdAsync(original.Id);
        var secondCopy = await database.Tasks.GetByIdAsync(original.Id);
        Assert.NotNull(firstCopy);
        Assert.NotNull(secondCopy);

        firstCopy!.UpdateDetails(
            "第一次更新",
            firstCopy.CategoryId,
            firstCopy.Priority,
            firstCopy.PlannedDate,
            firstCopy.PlannedStart,
            firstCopy.PlannedEnd,
            firstCopy.Deadline,
            firstCopy.Location,
            firstCopy.Description,
            firstCopy.Materials,
            firstCopy.Notes,
            PersistenceTestData.CreatedAtUtc.AddDays(1));
        var firstCommit = await database.Tasks.UpdateAsync(firstCopy, firstCopy.Version);

        secondCopy!.UpdateDetails(
            "过期更新",
            secondCopy.CategoryId,
            secondCopy.Priority,
            secondCopy.PlannedDate,
            secondCopy.PlannedStart,
            secondCopy.PlannedEnd,
            secondCopy.Deadline,
            secondCopy.Location,
            secondCopy.Description,
            secondCopy.Materials,
            secondCopy.Notes,
            PersistenceTestData.CreatedAtUtc.AddDays(2));

        var conflict = await Assert.ThrowsAsync<ScheduleAssistant.Application.Abstractions.Persistence.PersistenceConflictException>(
            () => database.Tasks.UpdateAsync(secondCopy, secondCopy.Version));
        var stored = await database.Tasks.GetByIdAsync(original.Id);

        Assert.Equal(2, firstCommit.NewVersion);
        Assert.Equal(2, firstCommit.Entity.Version);
        Assert.Equal(2, conflict.ActualVersion);
        Assert.NotNull(stored);
        Assert.Equal("第一次更新", stored!.Title);
        Assert.Equal(2, stored.Version);
    }
}
