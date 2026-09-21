using ScheduleAssistant.Application.Abstractions.Attachments;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class SqliteAttachmentCleanupQueueTests
{
    [Fact]
    public async Task Queue_ShouldPersistRetryAttemptAndRemoveCompletedItem()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var item = new AttachmentCleanupItem(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            AttachmentCleanupItemTypes.ManagedFile,
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            "task/file.txt",
            new DateTimeOffset(2026, 9, 21, 1, 0, 0, TimeSpan.Zero),
            0,
            null);

        await database.CleanupQueue.EnqueueAsync(item);
        var pending = Assert.Single(await database.CleanupQueue.GetPendingAsync(100));
        Assert.Equal(item.Id, pending.Id);
        Assert.Equal(0, pending.AttemptCount);

        await database.CleanupQueue.RecordFailureAsync(item.Id, "AccessDenied");
        pending = Assert.Single(await database.CleanupQueue.GetPendingAsync(100));
        Assert.Equal(1, pending.AttemptCount);
        Assert.Equal("AccessDenied", pending.LastErrorCode);

        await database.CleanupQueue.RemoveAsync(item.Id);
        Assert.Empty(await database.CleanupQueue.GetPendingAsync(100));
    }
}
