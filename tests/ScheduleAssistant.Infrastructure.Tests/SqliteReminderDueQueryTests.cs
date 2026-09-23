using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class SqliteReminderDueQueryTests
{
    [Fact]
    public async Task GetPendingDueAsync_WhenReadingAtBoundary_ShouldReturnOnlyDuePendingRemindersInOrder()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        await database.Categories.AddAsync(category);
        var task = PersistenceTestData.CreateTask(category.Id);
        await database.Tasks.AddAsync(task);

        var nowUtc = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var first = CreateReminder(task.Id, nowUtc.AddHours(-1));
        var boundary = CreateReminder(task.Id, nowUtc);
        var future = CreateReminder(task.Id, nowUtc.AddTicks(1));
        var delivered = CreateReminder(task.Id, nowUtc.AddMinutes(-2));
        delivered.MarkDelivered(nowUtc);
        var expired = CreateReminder(task.Id, nowUtc.AddMinutes(-3));
        expired.MarkExpired();
        var cancelled = CreateReminder(task.Id, nowUtc.AddMinutes(-4));
        cancelled.Cancel();

        foreach (var reminder in new[] { first, boundary, future, delivered, expired, cancelled })
        {
            await database.Reminders.AddAsync(reminder);
        }

        var due = await database.Reminders.GetPendingDueAsync(nowUtc);

        Assert.Equal(new[] { first.Id, boundary.Id }, due.Select(reminder => reminder.Id));
    }

    private static Reminder CreateReminder(Guid taskId, DateTimeOffset scheduledAtUtc)
    {
        return Reminder.Create(
            Guid.NewGuid(),
            taskId,
            -60,
            scheduledAtUtc,
            "sqlite-due-query-" + Guid.NewGuid().ToString("N"));
    }
}
