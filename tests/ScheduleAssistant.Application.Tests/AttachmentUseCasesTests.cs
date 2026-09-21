using ScheduleAssistant.Application.Abstractions.Attachments;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Attachments;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Application.Tests;

public sealed class AttachmentUseCasesTests
{
    private static readonly Guid TaskId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Import_WhenStoreSucceeds_ShouldPersistMetadataAndLeaveSourceUntouched()
    {
        var store = new FakeAttachmentStore();
        var repository = new FakeAttachmentRepository();
        var fixture = CreateUseCases(repository, store);

        var result = await fixture.UseCases.ImportAsync(
            new ImportAttachmentCommand(TaskId, "C:\\源文件\\会议 资料.pdf"));

        Assert.True(result.IsSuccess);
        var attachment = Assert.Single(repository.Items.Values);
        Assert.Equal("会议 资料.pdf", attachment.DisplayName);
        Assert.Equal(TaskId, attachment.TaskId);
        Assert.Equal("C:\\源文件\\会议 资料.pdf", store.LastSourcePath);
        Assert.Empty(store.DeletedPaths);
        Assert.Equal(64, attachment.Sha256!.Length);
    }

    [Fact]
    public async Task Import_WhenMetadataCommitFails_ShouldCompensateManagedFile()
    {
        var store = new FakeAttachmentStore();
        var repository = new FakeAttachmentRepository();
        var transactionFactory = new FakeTransactionFactory { ThrowOnCommit = true };
        var fixture = CreateUseCases(repository, store, transactionFactory);

        var result = await fixture.UseCases.ImportAsync(
            new ImportAttachmentCommand(TaskId, "source"));

        Assert.False(result.IsSuccess);
        Assert.Single(store.DeletedPaths);
        Assert.Empty(fixture.CleanupQueue.Items);
    }

    [Fact]
    public async Task Import_WhenCompensationDeleteFails_ShouldQueueManagedFileCleanup()
    {
        var store = new FakeAttachmentStore { ThrowOnDelete = true };
        var repository = new FakeAttachmentRepository();
        var transactionFactory = new FakeTransactionFactory { ThrowOnCommit = true };
        var fixture = CreateUseCases(repository, store, transactionFactory);

        var result = await fixture.UseCases.ImportAsync(
            new ImportAttachmentCommand(TaskId, "source"));

        Assert.False(result.IsSuccess);
        var queued = Assert.Single(fixture.CleanupQueue.Items);
        Assert.Equal(AttachmentCleanupItemTypes.ManagedFile, queued.ItemType);
        Assert.DoesNotContain("source", queued.ManagedRelativePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Remove_WhenManagedDeleteFails_ShouldCommitMetadataAndReturnQueuedWarning()
    {
        var attachment = CreateAttachment();
        var repository = new FakeAttachmentRepository(attachment);
        var store = new FakeAttachmentStore { ThrowOnDelete = true };
        var fixture = CreateUseCases(repository, store);

        var result = await fixture.UseCases.RemoveAsync(
            new RemoveAttachmentCommand(TaskId, attachment.Id));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.CleanupQueued);
        Assert.Empty(repository.Items);
        Assert.Single(fixture.CleanupQueue.Items);
        Assert.Contains(result.Warnings, warning => warning.Code == "Attachment.CleanupQueued");
    }

    [Fact]
    public async Task OpenRevealAndRename_WhenAttachmentBelongsToAnotherTask_ShouldNotTouchStore()
    {
        var attachment = CreateAttachment();
        var repository = new FakeAttachmentRepository(attachment);
        var store = new FakeAttachmentStore();
        var fixture = CreateUseCases(repository, store);
        var otherTask = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var open = await fixture.UseCases.OpenAsync(new OpenAttachmentCommand(otherTask, attachment.Id));
        var reveal = await fixture.UseCases.RevealAsync(new RevealAttachmentCommand(otherTask, attachment.Id));
        var rename = await fixture.UseCases.RenameAsync(
            new RenameAttachmentCommand(otherTask, attachment.Id, "新名称.pdf"));

        Assert.False(open.IsSuccess);
        Assert.False(reveal.IsSuccess);
        Assert.False(rename.IsSuccess);
        Assert.Empty(store.OpenedPaths);
        Assert.Empty(store.RevealedPaths);
        Assert.Empty(repository.UpdatedNames);
    }

    [Fact]
    public async Task TaskDeleted_WhenDirectoryCleanupFails_ShouldQueueAndStartupRetryIt()
    {
        var store = new FakeAttachmentStore { ThrowOnDeleteTaskDirectory = true };
        var repository = new FakeAttachmentRepository();
        var queue = new FakeCleanupQueue();
        var maintenance = new AttachmentMaintenanceService(
            repository,
            store,
            queue,
            new FixedTimeProvider(Now),
            () => Guid.Parse("44444444-4444-4444-4444-444444444444"));

        await maintenance.HandleTaskDeletedAsync(
            new TaskDeleted(TaskId, 2, Array.Empty<DateOnly>()));

        var queued = Assert.Single(queue.Items);
        Assert.Equal(AttachmentCleanupItemTypes.TaskDirectory, queued.ItemType);
        store.ThrowOnDeleteTaskDirectory = false;

        var report = await maintenance.RunStartupAsync();

        Assert.Equal(1, report.QueueItemsInspected);
        Assert.Equal(1, report.QueueItemsCleaned);
        Assert.Empty(queue.Items);
        Assert.Equal(TaskId, store.DeletedTaskDirectories.Last());
    }

    private static UseCaseFixture CreateUseCases(
        FakeAttachmentRepository repository,
        FakeAttachmentStore store,
        FakeTransactionFactory? transactionFactory = null)
    {
        var queue = new FakeCleanupQueue();
        var useCases = new AttachmentUseCases(
            repository,
            new FakeTaskRepository(CreateTask()),
            transactionFactory ?? new FakeTransactionFactory(),
            store,
            queue,
            new FixedTimeProvider(Now),
            () => Guid.Parse("55555555-5555-5555-5555-555555555555"));
        return new UseCaseFixture(useCases, queue);
    }

    private sealed record UseCaseFixture(
        AttachmentUseCases UseCases,
        FakeCleanupQueue CleanupQueue);

    private static TaskItem CreateTask()
    {
        return TaskItem.Create(TaskId, "附件任务", CategoryId, TaskPriority.Normal, Now);
    }

    private static Attachment CreateAttachment()
    {
        return Attachment.Create(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            TaskId,
            "资料.pdf",
            $"{TaskId:D}/66666666-6666-6666-6666-666666666666_资料.pdf",
            10,
            Now,
            ".pdf",
            "application/pdf",
            new string('a', 64));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeTaskRepository(TaskItem task) : ITaskRepository
    {
        public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<TaskItem?>(id == task.Id ? task : null);

        public Task<TaskItem?> GetByIdAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
            GetByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<TaskItem>> GetPlannedByDateAsync(DateOnly plannedOn, CancellationToken cancellationToken = default) => NotSupported<IReadOnlyList<TaskItem>>();
        public Task<IReadOnlyList<TaskItem>> GetByRangeAsync(DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default) => NotSupported<IReadOnlyList<TaskItem>>();
        public Task<IReadOnlyList<TaskItem>> GetTodayPendingAsync(DateOnly todayLocal, DateTimeOffset nowUtc, CancellationToken cancellationToken = default) => NotSupported<IReadOnlyList<TaskItem>>();
        public Task<IReadOnlyList<TaskItem>> GetUpcomingDeadlinesAsync(DateTimeOffset nowUtc, DateTimeOffset? untilUtc, CancellationToken cancellationToken = default) => NotSupported<IReadOnlyList<TaskItem>>();
        public Task<IReadOnlyList<TaskItem>> GetDeadlinesAsync(DateTimeOffset nowUtc, DateTimeOffset? untilUtc, bool includeOverdue, CancellationToken cancellationToken = default) => NotSupported<IReadOnlyList<TaskItem>>();
        public Task<IReadOnlyList<TaskItem>> SearchAsync(TaskSearchFilter filter, long offset, int limit, CancellationToken cancellationToken = default) => NotSupported<IReadOnlyList<TaskItem>>();
        public Task<long> CountSearchAsync(TaskSearchFilter filter, CancellationToken cancellationToken = default) => NotSupported<long>();
        public Task<PersistenceCommitResult<TaskItem>> AddAsync(TaskItem item, CancellationToken cancellationToken = default) => NotSupported<PersistenceCommitResult<TaskItem>>();
        public Task<PersistenceCommitResult<TaskItem>> AddAsync(TaskItem item, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) => NotSupported<PersistenceCommitResult<TaskItem>>();
        public Task<PersistenceCommitResult<TaskItem>> UpdateAsync(TaskItem item, long expectedVersion, CancellationToken cancellationToken = default) => NotSupported<PersistenceCommitResult<TaskItem>>();
        public Task<PersistenceCommitResult<TaskItem>> UpdateAsync(TaskItem item, long expectedVersion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) => NotSupported<PersistenceCommitResult<TaskItem>>();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromException(new NotSupportedException());
        public Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) => Task.FromException(new NotSupportedException());
        public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) => NotSupported<bool>();
        public Task<bool> DeleteAsync(Guid id, long expectedVersion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) => NotSupported<bool>();

        private static Task<T> NotSupported<T>() => Task.FromException<T>(new NotSupportedException());
    }

    private sealed class FakeAttachmentRepository(Attachment? initial = null) : IAttachmentRepository
    {
        public Dictionary<Guid, Attachment> Items { get; } = initial is null
            ? []
            : new Dictionary<Guid, Attachment> { [initial.Id] = initial };

        public List<string> UpdatedNames { get; } = [];

        public Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.GetValueOrDefault(id));

        public Task<IReadOnlyList<Attachment>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Attachment>>(Items.Values.Where(item => item.TaskId == taskId).ToArray());

        public Task<IReadOnlyList<Attachment>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Attachment>>(Items.Values.ToArray());

        public Task AddAsync(Attachment attachment, CancellationToken cancellationToken = default)
        {
            Items[attachment.Id] = attachment;
            return Task.CompletedTask;
        }

        public Task AddAsync(Attachment attachment, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
        {
            Items[attachment.Id] = attachment;
            return Task.CompletedTask;
        }

        public Task UpdateDisplayNameAsync(Attachment attachment, CancellationToken cancellationToken = default)
        {
            Items[attachment.Id] = attachment;
            UpdatedNames.Add(attachment.DisplayName);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.Remove(id);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
        {
            Items.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTransactionFactory : IPersistenceTransactionFactory
    {
        public bool ThrowOnCommit { get; init; }

        public Task<IPersistenceTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IPersistenceTransaction>(new FakeTransaction(ThrowOnCommit));
    }

    private sealed class FakeTransaction(bool throwOnCommit) : IPersistenceTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            throwOnCommit
                ? Task.FromException(new InvalidOperationException("simulated database failure"))
                : Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeAttachmentStore : IAttachmentStore
    {
        public string? LastSourcePath { get; private set; }
        public bool ThrowOnDelete { get; init; }
        public bool ThrowOnDeleteTaskDirectory { get; set; }
        public List<string> DeletedPaths { get; } = [];
        public List<string> OpenedPaths { get; } = [];
        public List<string> RevealedPaths { get; } = [];
        public List<Guid> DeletedTaskDirectories { get; } = [];

        public Task<StoredAttachment> ImportAsync(Guid taskId, Guid attachmentId, string sourcePath, CancellationToken cancellationToken = default)
        {
            LastSourcePath = sourcePath;
            return Task.FromResult(new StoredAttachment(
                attachmentId,
                taskId,
                "会议 资料.pdf",
                $"{taskId:D}/{attachmentId:D}_会议 资料.pdf",
                ".pdf",
                "application/pdf",
                128,
                new string('a', 64)));
        }

        public Task OpenAsync(string managedRelativePath, CancellationToken cancellationToken = default)
        {
            OpenedPaths.Add(managedRelativePath);
            return Task.CompletedTask;
        }

        public Task RevealAsync(string managedRelativePath, CancellationToken cancellationToken = default)
        {
            RevealedPaths.Add(managedRelativePath);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string managedRelativePath, CancellationToken cancellationToken = default)
        {
            DeletedPaths.Add(managedRelativePath);
            return ThrowOnDelete
                ? Task.FromException(new IOException("simulated file failure"))
                : Task.CompletedTask;
        }

        public Task DeleteTaskDirectoryAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            DeletedTaskDirectories.Add(taskId);
            return ThrowOnDeleteTaskDirectory
                ? Task.FromException(new IOException("simulated directory failure"))
                : Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListManagedFilesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class FakeCleanupQueue : IAttachmentCleanupQueue
    {
        public List<AttachmentCleanupItem> Items { get; } = [];

        public Task EnqueueAsync(AttachmentCleanupItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AttachmentCleanupItem>> GetPendingAsync(int maximumItems, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AttachmentCleanupItem>>(Items.Take(maximumItems).ToArray());

        public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(Guid id, string errorCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
