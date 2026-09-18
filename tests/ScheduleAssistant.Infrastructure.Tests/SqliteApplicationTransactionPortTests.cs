using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class SqliteApplicationTransactionPortTests
{
    [Fact]
    public async Task TransactionReadsAndPendingCancellation_ShouldNotOverwriteDeliveredReminders()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var task = PersistenceTestData.CreateTask(
            category.Id,
            plannedDate: new DateOnly(2026, 1, 2),
            deadline: PersistenceTestData.CreateDeadline(new DateOnly(2026, 1, 3)));
        await database.Tasks.AddAsync(task);
        var pending = Reminder.Create(
            Guid.NewGuid(),
            task.Id,
            -1_440,
            PersistenceTestData.CreatedAtUtc,
            "application-pending");
        var delivered = Reminder.Rehydrate(
            Guid.NewGuid(),
            task.Id,
            0,
            PersistenceTestData.CreatedAtUtc.AddDays(1),
            PersistenceTestData.CreatedAtUtc.AddDays(1),
            ReminderStatus.Delivered,
            "application-delivered",
            errorCode: null);
        await database.Reminders.AddAsync(pending);
        await database.Reminders.AddAsync(delivered);

        await using (var transaction = await database.TransactionFactory.BeginAsync())
        {
            var taskInTransaction = await database.Tasks.GetByIdAsync(task.Id, transaction);
            var remindersInTransaction = await database.Reminders.GetByTaskIdAsync(task.Id, transaction);
            var cancelled = await database.Reminders.CancelPendingByTaskIdAsync(task.Id, transaction);
            await transaction.CommitAsync();

            Assert.Equal(task.Id, taskInTransaction!.Id);
            Assert.Equal(2, remindersInTransaction.Count);
            Assert.Equal(1, cancelled);
        }

        var stored = await database.Reminders.GetByTaskIdAsync(task.Id);
        Assert.Equal(ReminderStatus.Cancelled, Assert.Single(stored, reminder => reminder.Id == pending.Id).Status);
        Assert.Equal(ReminderStatus.Delivered, Assert.Single(stored, reminder => reminder.Id == delivered.Id).Status);
    }

    [Fact]
    public async Task ConditionalDelete_WhenExpectedVersionIsStale_ShouldConflictWithoutDeleting()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var task = PersistenceTestData.CreateTask(category.Id);
        await database.Tasks.AddAsync(task);

        await Assert.ThrowsAsync<PersistenceConflictException>(() =>
            database.Tasks.DeleteAsync(task.Id, task.Version + 1));

        Assert.NotNull(await database.Tasks.GetByIdAsync(task.Id));
        Assert.True(await database.Tasks.DeleteAsync(task.Id, task.Version));
        Assert.Null(await database.Tasks.GetByIdAsync(task.Id));
    }
}
