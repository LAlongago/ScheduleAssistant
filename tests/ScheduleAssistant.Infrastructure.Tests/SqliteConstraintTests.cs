using Microsoft.Data.Sqlite;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class SqliteConstraintTests
{
    [Fact]
    public async Task DatabaseConstraints_ShouldProtectForeignKeysAndDeduplicateRecurrenceAndReminders()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);

        var missingCategoryTask = TaskItem.Create(
            Guid.NewGuid(),
            "外键任务",
            Guid.NewGuid(),
            PersistenceTestData.CreatedAtUtc);
        await Assert.ThrowsAsync<SqliteException>(() => database.Tasks.AddAsync(missingCategoryTask));

        var series = PersistenceTestData.CreateSeries(category.Id);
        await database.Series.AddAsync(series);
        var occurrence = new OccurrenceDate(new DateOnly(2026, 2, 28));
        var firstOccurrence = PersistenceTestData.CreateTask(
            category.Id,
            plannedDate: occurrence.Date,
            seriesId: series.Id,
            occurrenceDate: occurrence);
        var duplicateOccurrence = PersistenceTestData.CreateTask(
            category.Id,
            plannedDate: occurrence.Date,
            seriesId: series.Id,
            occurrenceDate: occurrence);
        await database.Tasks.AddAsync(firstOccurrence);
        await Assert.ThrowsAsync<SqliteException>(() => database.Tasks.AddAsync(duplicateOccurrence));

        var exclusion = new RecurrenceExclusion(series.Id, occurrence.Date);
        await database.Exclusions.AddAsync(exclusion);
        await Assert.ThrowsAsync<SqliteException>(() => database.Exclusions.AddAsync(exclusion));

        var reminderOne = Reminder.Create(
            Guid.NewGuid(),
            firstOccurrence.Id,
            -1_440,
            PersistenceTestData.CreatedAtUtc,
            "same-node");
        var reminderTwo = Reminder.Create(
            Guid.NewGuid(),
            firstOccurrence.Id,
            0,
            PersistenceTestData.CreatedAtUtc.AddHours(1),
            "different-node");
        var duplicateReminder = Reminder.Create(
            Guid.NewGuid(),
            firstOccurrence.Id,
            60,
            PersistenceTestData.CreatedAtUtc.AddHours(2),
            "same-node");
        await database.Reminders.AddAsync(reminderOne);
        await database.Reminders.AddAsync(reminderTwo);
        await Assert.ThrowsAsync<SqliteException>(() => database.Reminders.AddAsync(duplicateReminder));
        Assert.Equal(2, (await database.Reminders.GetByTaskIdAsync(firstOccurrence.Id)).Count);

        await Assert.ThrowsAsync<SqliteException>(() => database.Categories.DeleteAsync(category.Id));
        Assert.NotNull(await database.Tasks.GetByIdAsync(firstOccurrence.Id));

        firstOccurrence.Complete(PersistenceTestData.CreatedAtUtc.AddDays(1));
        var completed = await database.Tasks.UpdateAsync(firstOccurrence, firstOccurrence.Version);
        Assert.Equal(2, completed.NewVersion);
        await Assert.ThrowsAsync<SqliteException>(() => database.Series.DeleteAsync(series.Id));
        var completedReload = await database.Tasks.GetByIdAsync(firstOccurrence.Id);
        Assert.Equal(WorkflowStatus.Completed, completedReload!.WorkflowStatus);
    }

    [Fact]
    public async Task CrossRepositoryTransaction_WhenLaterInsertFails_ShouldRollbackEarlierWrites()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var task = PersistenceTestData.CreateTask(category.Id);
        var firstReminder = Reminder.Create(
            Guid.NewGuid(),
            task.Id,
            -60,
            PersistenceTestData.CreatedAtUtc,
            "rollback-node");
        var duplicateReminder = Reminder.Create(
            Guid.NewGuid(),
            task.Id,
            0,
            PersistenceTestData.CreatedAtUtc.AddHours(1),
            "rollback-node");

        await using var transaction = await database.TransactionFactory.BeginAsync();
        await database.Tasks.AddAsync(task, transaction);
        await database.Reminders.AddAsync(firstReminder, transaction);
        await Assert.ThrowsAsync<SqliteException>(() => database.Reminders.AddAsync(duplicateReminder, transaction));
        await transaction.RollbackAsync();

        Assert.Null(await database.Tasks.GetByIdAsync(task.Id));
        Assert.Null(await database.Reminders.GetByIdAsync(firstReminder.Id));
    }
}
