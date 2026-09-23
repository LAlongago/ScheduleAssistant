using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Tests;

internal sealed class TaskUseCaseTestContext : IAsyncDisposable
{
    public TaskUseCaseTestContext()
    {
        Store = new InMemoryTaskStore();
        TaskRepository = new InMemoryTaskRepository(Store);
        CategoryRepository = new InMemoryCategoryRepository(Store);
        ReminderRepository = new InMemoryReminderRepository(Store);
        TransactionFactory = new InMemoryTransactionFactory(Store);
        Publisher = new RecordingEventPublisher();
        UseCases = new TaskUseCases(
            TaskRepository,
            CategoryRepository,
            ReminderRepository,
            TransactionFactory,
            Publisher,
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            idFactory: () => Guid.NewGuid());
    }

    public InMemoryTaskStore Store { get; }

    public InMemoryTaskRepository TaskRepository { get; }

    public InMemoryCategoryRepository CategoryRepository { get; }

    public InMemoryReminderRepository ReminderRepository { get; }

    public InMemoryTransactionFactory TransactionFactory { get; }

    public RecordingEventPublisher Publisher { get; }

    public TaskUseCases UseCases { get; }

    public Category SeedCategory(bool archived = false)
    {
        var category = Category.Create(
            Guid.NewGuid(),
            "科研",
            "#123456",
            1,
            new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero));
        if (archived)
        {
            category.UpdateDetails(category.Name, category.ColorHex, category.SortOrder, true, category.UpdatedAtUtc);
        }

        Store.Categories[category.Id] = Clone(category);
        return category;
    }

    public Task<Application.Common.ApplicationResult<TaskDto>> CreateTaskAsync(Guid categoryId)
    {
        return UseCases.CreateAsync(new CreateTaskCommand(new TaskDraft(
            "Test task",
            categoryId,
            Deadline: new DeadlineInput(
                new DateOnly(2026, 1, 3),
                new TimeOnly(12, 0),
                "China Standard Time"))));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    internal static TaskItem Clone(TaskItem task)
    {
        return TaskItem.Rehydrate(
            task.Id,
            task.Title,
            task.CategoryId,
            task.Priority,
            task.WorkflowStatus,
            task.CreatedAtUtc,
            task.UpdatedAtUtc,
            task.Version,
            task.PlannedDate,
            task.PlannedStart,
            task.PlannedEnd,
            task.Deadline,
            task.Location,
            task.Description,
            task.Materials,
            task.Notes,
            task.SeriesId,
            task.OccurrenceDate,
            task.IsOccurrenceOverride,
            task.CompletedAtUtc);
    }

    internal static Category Clone(Category category)
    {
        return Category.Rehydrate(
            category.Id,
            category.Name,
            category.ColorHex,
            category.SortOrder,
            category.IsBuiltIn,
            category.IsArchived,
            category.CreatedAtUtc,
            category.UpdatedAtUtc,
            category.Version);
    }

    internal static Reminder Clone(Reminder reminder)
    {
        return Reminder.Rehydrate(
            reminder.Id,
            reminder.TaskId,
            reminder.RelativeOffsetMinutes,
            reminder.ScheduledAtUtc,
            reminder.DeliveredAtUtc,
            reminder.Status,
            reminder.DeduplicationKey,
            reminder.ErrorCode);
    }
}

internal sealed class InMemoryTaskStore
{
    public Dictionary<Guid, TaskItem> Tasks { get; } = new();

    public Dictionary<Guid, Category> Categories { get; } = new();

    public Dictionary<Guid, List<Reminder>> Reminders { get; } = new();

    public IReadOnlyList<Reminder> RemindersFor(Guid taskId)
    {
        return Reminders.TryGetValue(taskId, out var reminders)
            ? reminders.Select(TaskUseCaseTestContext.Clone).ToArray()
            : Array.Empty<Reminder>();
    }

    public void AddDeliveredReminder(Guid taskId)
    {
        var delivered = Reminder.Rehydrate(
            Guid.NewGuid(),
            taskId,
            -60,
            new DateTimeOffset(2025, 12, 31, 23, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ReminderStatus.Delivered,
            "delivered-node-" + Guid.NewGuid().ToString("N"),
            errorCode: null);
        Reminders[taskId].Add(delivered);
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
}

internal sealed class RecordingEventPublisher : IApplicationEventPublisher
{
    public List<IApplicationEvent> Events { get; } = new();

    public Exception? ExceptionToThrow { get; set; }

    public Task PublishAsync(IApplicationEvent applicationEvent, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        Events.Add(applicationEvent);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryTransactionFactory : IPersistenceTransactionFactory
{
    private readonly InMemoryTaskStore _store;

    public InMemoryTransactionFactory(InMemoryTaskStore store)
    {
        _store = store;
    }

    public Exception? BeginFailure { get; set; }

    public Exception? CommitFailure { get; set; }

    public InMemoryTransaction? LastTransaction { get; private set; }

    public Task<IPersistenceTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (BeginFailure is not null)
        {
            throw BeginFailure;
        }

        LastTransaction = new InMemoryTransaction(_store, CommitFailure);
        return Task.FromResult<IPersistenceTransaction>(LastTransaction);
    }
}

internal sealed class InMemoryTransaction : IPersistenceTransaction
{
    private readonly InMemoryTaskStore _store;
    private readonly Exception? _commitFailure;
    private bool _completed;

    public InMemoryTransaction(InMemoryTaskStore store, Exception? commitFailure)
    {
        _store = store;
        _commitFailure = commitFailure;
        Working = CloneStore(store);
    }

    public InMemoryTaskStore Working { get; }

    public bool RollbackCalled { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_commitFailure is not null)
        {
            throw _commitFailure;
        }

        ReplaceStore(_store, Working);
        _completed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RollbackCalled = true;
        _completed = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!_completed)
        {
            RollbackCalled = true;
            _completed = true;
        }

        return ValueTask.CompletedTask;
    }

    private static InMemoryTaskStore CloneStore(InMemoryTaskStore source)
    {
        var clone = new InMemoryTaskStore();
        foreach (var pair in source.Tasks)
        {
            clone.Tasks[pair.Key] = TaskUseCaseTestContext.Clone(pair.Value);
        }

        foreach (var pair in source.Categories)
        {
            clone.Categories[pair.Key] = TaskUseCaseTestContext.Clone(pair.Value);
        }

        foreach (var pair in source.Reminders)
        {
            clone.Reminders[pair.Key] = pair.Value.Select(TaskUseCaseTestContext.Clone).ToList();
        }

        return clone;
    }

    private static void ReplaceStore(InMemoryTaskStore destination, InMemoryTaskStore source)
    {
        destination.Tasks.Clear();
        destination.Categories.Clear();
        destination.Reminders.Clear();
        foreach (var pair in source.Tasks)
        {
            destination.Tasks[pair.Key] = TaskUseCaseTestContext.Clone(pair.Value);
        }

        foreach (var pair in source.Categories)
        {
            destination.Categories[pair.Key] = TaskUseCaseTestContext.Clone(pair.Value);
        }

        foreach (var pair in source.Reminders)
        {
            destination.Reminders[pair.Key] = pair.Value.Select(TaskUseCaseTestContext.Clone).ToList();
        }
    }
}

internal sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly InMemoryTaskStore _store;

    public InMemoryTaskRepository(InMemoryTaskStore store)
    {
        _store = store;
    }

    public Exception? ExceptionToThrow { get; set; }

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        return Task.FromResult(_store.Tasks.TryGetValue(id, out var task) ? TaskUseCaseTestContext.Clone(task) : null);
    }

    public Task<TaskItem?> GetByIdAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        var working = ((InMemoryTransaction)transaction).Working;
        return Task.FromResult(working.Tasks.TryGetValue(id, out var task) ? TaskUseCaseTestContext.Clone(task) : null);
    }

    public Task<IReadOnlyList<TaskItem>> GetPlannedByDateAsync(DateOnly plannedOn, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        return Task.FromResult<IReadOnlyList<TaskItem>>(_store.Tasks.Values.Where(t => t.PlannedDate == plannedOn).Select(TaskUseCaseTestContext.Clone).ToArray());
    }

    public Task<IReadOnlyList<TaskItem>> GetByRangeAsync(DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        return Task.FromResult<IReadOnlyList<TaskItem>>(_store.Tasks.Values
            .Where(task =>
                task.PlannedDate is DateOnly plannedDate && plannedDate >= rangeStart && plannedDate <= rangeEnd
                || task.Deadline?.LocalDate is DateOnly deadlineDate && deadlineDate >= rangeStart && deadlineDate <= rangeEnd)
            .Select(TaskUseCaseTestContext.Clone)
            .ToArray());
    }

    public Task<IReadOnlyList<TaskItem>> GetTodayPendingAsync(DateOnly todayLocal, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        var normalizedNow = nowUtc.ToUniversalTime();
        return Task.FromResult<IReadOnlyList<TaskItem>>(_store.Tasks.Values
            .Where(task => task.WorkflowStatus != WorkflowStatus.Completed)
            .Where(task =>
                task.PlannedDate is DateOnly plannedDate && plannedDate <= todayLocal
                || task.DeadlineUtc is DateTimeOffset deadline && deadline < normalizedNow
                || task.Deadline?.LocalDate == todayLocal)
            .Select(TaskUseCaseTestContext.Clone)
            .ToArray());
    }

    public Task<IReadOnlyList<TaskItem>> GetUpcomingDeadlinesAsync(DateTimeOffset nowUtc, DateTimeOffset? untilUtc, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        return Task.FromResult<IReadOnlyList<TaskItem>>(_store.Tasks.Values.Where(t => t.WorkflowStatus != WorkflowStatus.Completed && t.DeadlineUtc >= nowUtc && (!untilUtc.HasValue || t.DeadlineUtc <= untilUtc)).Select(TaskUseCaseTestContext.Clone).ToArray());
    }

    public Task<IReadOnlyList<TaskItem>> GetDeadlinesAsync(DateTimeOffset nowUtc, DateTimeOffset? untilUtc, bool includeOverdue, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        var normalizedNow = nowUtc.ToUniversalTime();
        return Task.FromResult<IReadOnlyList<TaskItem>>(_store.Tasks.Values
            .Where(task => task.WorkflowStatus != WorkflowStatus.Completed && task.DeadlineUtc.HasValue)
            .Where(task =>
                includeOverdue && task.DeadlineUtc!.Value < normalizedNow
                || task.DeadlineUtc!.Value >= normalizedNow
                && (!untilUtc.HasValue || task.DeadlineUtc.Value <= untilUtc.Value.ToUniversalTime()))
            .Select(TaskUseCaseTestContext.Clone)
            .ToArray());
    }

    public Task<IReadOnlyList<TaskItem>> SearchAsync(TaskSearchFilter filter, long offset, int limit, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var ordered = SearchItems(filter).ToArray();
        var page = offset >= ordered.Length
            ? Array.Empty<TaskItem>()
            : ordered.Skip((int)Math.Min(offset, int.MaxValue)).Take(limit).Select(TaskUseCaseTestContext.Clone).ToArray();
        return Task.FromResult<IReadOnlyList<TaskItem>>(page);
    }

    public Task<long> CountSearchAsync(TaskSearchFilter filter, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        return Task.FromResult(SearchItems(filter).LongCount());
    }

    private IEnumerable<TaskItem> SearchItems(TaskSearchFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var normalizedNow = filter.NowUtc.ToUniversalTime();
        var keyword = string.IsNullOrWhiteSpace(filter.Keyword) ? null : filter.Keyword;
        return _store.Tasks.Values
            .Where(task => !filter.CategoryId.HasValue || task.CategoryId == filter.CategoryId.Value)
            .Where(task => !filter.Priority.HasValue || task.Priority == filter.Priority.Value)
            .Where(task => !filter.WorkflowStatus.HasValue || task.WorkflowStatus == filter.WorkflowStatus.Value)
            .Where(task =>
            {
                var isOverdue = task.WorkflowStatus != WorkflowStatus.Completed
                    && task.DeadlineUtc is DateTimeOffset deadline
                    && deadline < normalizedNow;
                return !filter.IsOverdue.HasValue || filter.IsOverdue.Value == isOverdue;
            })
            .Where(task => keyword is null ||
                Contains(task.Title, keyword)
                || Contains(task.Location, keyword)
                || Contains(task.Description, keyword)
                || Contains(task.Materials, keyword)
                || Contains(task.Notes, keyword))
            .OrderBy(task => task.PlannedDate.HasValue ? 0 : 1)
            .ThenBy(task => task.PlannedDate)
            .ThenBy(task => task.PlannedStart.HasValue ? 1 : 0)
            .ThenBy(task => task.PlannedStart)
            .ThenBy(task => task.DeadlineUtc.HasValue ? 0 : 1)
            .ThenBy(task => task.DeadlineUtc)
            .ThenByDescending(task => task.Priority)
            .ThenBy(task => task.CreatedAtUtc)
            .ThenBy(task => task.Id);
    }

    private static bool Contains(string? value, string keyword) =>
        value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;

    public Task<PersistenceCommitResult<TaskItem>> AddAsync(TaskItem item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<PersistenceCommitResult<TaskItem>> AddAsync(TaskItem item, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        var working = ((InMemoryTransaction)transaction).Working;
        if (working.Tasks.ContainsKey(item.Id))
        {
            throw new PersistenceFailureException(PersistenceFailureKind.Constraint, "task-add");
        }

        var stored = TaskUseCaseTestContext.Clone(item);
        working.Tasks.Add(stored.Id, stored);
        return Task.FromResult(new PersistenceCommitResult<TaskItem>(TaskUseCaseTestContext.Clone(stored), stored.Version));
    }

    public Task<PersistenceCommitResult<TaskItem>> UpdateAsync(TaskItem item, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<PersistenceCommitResult<TaskItem>> UpdateAsync(TaskItem item, long expectedVersion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        var working = ((InMemoryTransaction)transaction).Working;
        if (!working.Tasks.TryGetValue(item.Id, out var stored))
        {
            throw new PersistenceNotFoundException(nameof(TaskItem), item.Id);
        }

        if (stored.Version != expectedVersion)
        {
            throw new PersistenceConflictException(nameof(TaskItem), item.Id, expectedVersion, stored.Version);
        }

        var next = TaskItem.Rehydrate(
            item.Id,
            item.Title,
            item.CategoryId,
            item.Priority,
            item.WorkflowStatus,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            expectedVersion + 1,
            item.PlannedDate,
            item.PlannedStart,
            item.PlannedEnd,
            item.Deadline,
            item.Location,
            item.Description,
            item.Materials,
            item.Notes,
            item.SeriesId,
            item.OccurrenceDate,
            item.IsOccurrenceOverride,
            item.CompletedAtUtc);
        working.Tasks[item.Id] = next;
        return Task.FromResult(new PersistenceCommitResult<TaskItem>(TaskUseCaseTestContext.Clone(next), next.Version));
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        ThrowIfConfigured();
        var working = ((InMemoryTransaction)transaction).Working;
        if (!working.Tasks.TryGetValue(id, out var stored))
        {
            throw new PersistenceNotFoundException(nameof(TaskItem), id);
        }

        if (stored.Version != expectedVersion)
        {
            throw new PersistenceConflictException(nameof(TaskItem), id, expectedVersion, stored.Version);
        }

        working.Tasks.Remove(id);
        working.Reminders.Remove(id);
        return Task.FromResult(true);
    }

    private void ThrowIfConfigured()
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }
    }
}

internal sealed class InMemoryCategoryRepository : ICategoryRepository
{
    private readonly InMemoryTaskStore _store;

    public InMemoryCategoryRepository(InMemoryTaskStore store)
    {
        _store = store;
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Categories.TryGetValue(id, out var category) ? TaskUseCaseTestContext.Clone(category) : null);

    public Task<Category?> GetByIdAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        var working = ((InMemoryTransaction)transaction).Working;
        return Task.FromResult(working.Categories.TryGetValue(id, out var category) ? TaskUseCaseTestContext.Clone(category) : null);
    }

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Category>>(_store.Categories.Values.Select(TaskUseCaseTestContext.Clone).ToArray());

    public Task<PersistenceCommitResult<Category>> AddAsync(Category category, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<PersistenceCommitResult<Category>> AddAsync(Category category, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<PersistenceCommitResult<Category>> UpdateAsync(Category category, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<PersistenceCommitResult<Category>> UpdateAsync(Category category, long expectedVersion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class InMemoryReminderRepository : IReminderRepository
{
    private readonly InMemoryTaskStore _store;

    public InMemoryReminderRepository(InMemoryTaskStore store)
    {
        _store = store;
    }

    public Task<Reminder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Reminders.Values.SelectMany(items => items).FirstOrDefault(r => r.Id == id) is { } reminder ? TaskUseCaseTestContext.Clone(reminder) : null);

    public Task<IReadOnlyList<Reminder>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.RemindersFor(taskId));

    public Task<IReadOnlyList<Reminder>> GetByTaskIdAsync(Guid taskId, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        var working = ((InMemoryTransaction)transaction).Working;
        return Task.FromResult<IReadOnlyList<Reminder>>(working.Reminders.TryGetValue(taskId, out var reminders) ? reminders.Select(TaskUseCaseTestContext.Clone).ToArray() : Array.Empty<Reminder>());
    }

    public Task<Reminder?> GetNextPendingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<Reminder?>(_store.Reminders.Values.SelectMany(items => items).Where(r => r.Status == ReminderStatus.Pending).OrderBy(r => r.ScheduledAtUtc).Select(TaskUseCaseTestContext.Clone).FirstOrDefault());

    public Task<IReadOnlyList<Reminder>> GetPendingDueAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Reminder>>(_store.Reminders.Values
            .SelectMany(items => items)
            .Where(reminder => reminder.Status == ReminderStatus.Pending && reminder.ScheduledAtUtc <= nowUtc)
            .OrderBy(reminder => reminder.ScheduledAtUtc)
            .ThenBy(reminder => reminder.Id)
            .Select(TaskUseCaseTestContext.Clone)
            .ToArray());

    public Task AddAsync(Reminder reminder, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task AddAsync(Reminder reminder, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        var working = ((InMemoryTransaction)transaction).Working;
        if (working.Reminders.Values.SelectMany(items => items).Any(existing => existing.DeduplicationKey == reminder.DeduplicationKey))
        {
            throw new PersistenceFailureException(PersistenceFailureKind.Constraint, "reminder-add");
        }

        if (!working.Reminders.TryGetValue(reminder.TaskId, out var reminders))
        {
            reminders = new List<Reminder>();
            working.Reminders.Add(reminder.TaskId, reminders);
        }

        reminders.Add(TaskUseCaseTestContext.Clone(reminder));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default)
    {
        if (!_store.Reminders.TryGetValue(reminder.TaskId, out var reminders))
        {
            throw new KeyNotFoundException($"Reminder '{reminder.Id}' was not found.");
        }

        var index = reminders.FindIndex(existing => existing.Id == reminder.Id);
        if (index < 0)
        {
            throw new KeyNotFoundException($"Reminder '{reminder.Id}' was not found.");
        }

        reminders[index] = TaskUseCaseTestContext.Clone(reminder);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Reminder reminder, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        var working = ((InMemoryTransaction)transaction).Working;
        foreach (var reminders in working.Reminders.Values)
        {
            if (reminders.RemoveAll(reminder => reminder.Id == id) != 0)
            {
                break;
            }
        }

        return Task.CompletedTask;
    }

    public Task<int> CancelPendingByTaskIdAsync(Guid taskId, IPersistenceTransaction transaction, CancellationToken cancellationToken = default)
    {
        var working = ((InMemoryTransaction)transaction).Working;
        if (!working.Reminders.TryGetValue(taskId, out var reminders))
        {
            return Task.FromResult(0);
        }

        var count = 0;
        for (var index = 0; index < reminders.Count; index++)
        {
            if (reminders[index].Status != ReminderStatus.Pending)
            {
                continue;
            }

            var reminder = reminders[index];
            reminders[index] = Reminder.Rehydrate(
                reminder.Id,
                reminder.TaskId,
                reminder.RelativeOffsetMinutes,
                reminder.ScheduledAtUtc,
                deliveredAtUtc: null,
                ReminderStatus.Cancelled,
                reminder.DeduplicationKey,
                errorCode: null);
            count++;
        }

        return Task.FromResult(count);
    }
}
