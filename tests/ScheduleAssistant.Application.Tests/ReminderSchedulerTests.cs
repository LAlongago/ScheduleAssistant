using Microsoft.Extensions.Logging.Abstractions;
using ScheduleAssistant.Application.Reminders;
using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Application.Tests;

public sealed class ReminderSchedulerTests
{
    [Fact]
    public async Task StartAsync_WhenPendingRemindersExist_ShouldScheduleEarliestAndPersistDelivery()
    {
        await using var context = new ReminderSchedulerTestContext();
        var task = context.AddTask();
        var first = context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddMinutes(5));
        var second = context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddMinutes(20));

        await context.Scheduler.StartAsync();

        Assert.Equal(TimeSpan.FromMinutes(5), context.TimerFactory.Timer!.ScheduledDelay);
        context.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await context.TimerFactory.Timer.FireAsync();

        var notification = Assert.Single(context.NotificationService.Attempts);
        Assert.Equal(task.Id, notification.TaskId);
        Assert.Equal(first.DeduplicationKey, notification.DeduplicationKey);
        Assert.Equal(ReminderStatus.Delivered, (await context.ReminderRepository.GetByIdAsync(first.Id))!.Status);
        Assert.Equal(context.TimeProvider.GetUtcNow(), (await context.ReminderRepository.GetByIdAsync(first.Id))!.DeliveredAtUtc);
        Assert.Equal(ReminderStatus.Pending, (await context.ReminderRepository.GetByIdAsync(second.Id))!.Status);
        Assert.Equal(TimeSpan.FromMinutes(15), context.TimerFactory.Timer.ScheduledDelay);
    }

    [Fact]
    public async Task RescheduleAsync_WhenAnEarlierReminderIsAdded_ShouldMoveOneShotTimerEarlier()
    {
        await using var context = new ReminderSchedulerTestContext();
        var task = context.AddTask();
        context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddMinutes(10));

        await context.Scheduler.StartAsync();
        Assert.Equal(TimeSpan.FromMinutes(10), context.TimerFactory.Timer!.ScheduledDelay);

        context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddMinutes(2));
        await context.Scheduler.RescheduleAsync();

        Assert.Equal(TimeSpan.FromMinutes(2), context.TimerFactory.Timer.ScheduledDelay);
    }

    [Fact]
    public async Task TimerCallback_WhenRepeatedConcurrently_ShouldDeliverReminderOnlyOnce()
    {
        await using var context = new ReminderSchedulerTestContext();
        var task = context.AddTask();
        var reminder = context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddMinutes(1));
        await context.Scheduler.StartAsync();

        context.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        await Task.WhenAll(
            context.TimerFactory.Timer!.FireAsync(),
            context.TimerFactory.Timer.FireAsync());

        Assert.Single(context.NotificationService.Attempts);
        Assert.Equal(ReminderStatus.Delivered, (await context.ReminderRepository.GetByIdAsync(reminder.Id))!.Status);
    }

    [Fact]
    public async Task StartAsync_WhenDueRemindersStraddleTwentyFourHours_ShouldDeliverBoundaryAndExpireOlderReminder()
    {
        await using var context = new ReminderSchedulerTestContext();
        var task = context.AddTask();
        var now = context.TimeProvider.GetUtcNow();
        var boundary = context.AddReminder(task.Id, now.AddHours(-24));
        var tooOld = context.AddReminder(task.Id, now.AddHours(-24).AddTicks(-1));

        await context.Scheduler.StartAsync();

        var delivered = await context.ReminderRepository.GetByIdAsync(boundary.Id);
        var expired = await context.ReminderRepository.GetByIdAsync(tooOld.Id);
        Assert.Equal(ReminderStatus.Delivered, delivered!.Status);
        Assert.Equal(now, delivered.DeliveredAtUtc);
        Assert.Equal(ReminderStatus.Expired, expired!.Status);
        Assert.True(Assert.Single(context.NotificationService.Attempts).IsDelayed);
    }

    [Fact]
    public async Task StartAsync_WhenNotificationProviderIsUnavailable_ShouldPreserveDueReminderAndSleepWithoutPolling()
    {
        await using var context = new ReminderSchedulerTestContext();
        var task = context.AddTask();
        var reminder = context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddDays(-2));
        context.NotificationService.IsAvailable = false;

        await context.Scheduler.StartAsync();

        var persisted = await context.ReminderRepository.GetByIdAsync(reminder.Id);
        Assert.Equal(ReminderStatus.Pending, persisted!.Status);
        Assert.Empty(context.NotificationService.Attempts);
        Assert.Equal(1, context.NotificationService.CapabilityCheckCount);
        Assert.Equal(0, context.ReminderRepository.PendingDueQueryCount);
        Assert.Equal(0, context.TimerFactory.CreateCount);
    }

    [Fact]
    public async Task ResumeAsync_WhenNotificationProviderBecomesAvailable_ShouldCompensatePendingReminder()
    {
        await using var context = new ReminderSchedulerTestContext();
        var task = context.AddTask();
        var reminder = context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddHours(-1));
        context.NotificationService.IsAvailable = false;

        await context.Scheduler.StartAsync();
        context.NotificationService.IsAvailable = true;

        await context.Scheduler.ResumeAsync();

        var persisted = await context.ReminderRepository.GetByIdAsync(reminder.Id);
        Assert.Equal(ReminderStatus.Delivered, persisted!.Status);
        Assert.Single(context.NotificationService.Attempts);
        Assert.True(context.TimerFactory.CreateCount > 0);
    }

    [Fact]
    public async Task StartAsync_WhenTaskIsCompletedOrMissing_ShouldCancelWithoutNotification()
    {
        await using var context = new ReminderSchedulerTestContext();
        var completedTask = context.AddTask(completed: true);
        var missingTaskId = Guid.NewGuid();
        var completedReminder = context.AddReminder(completedTask.Id, context.TimeProvider.GetUtcNow().AddMinutes(-1));
        var missingTaskReminder = context.AddReminder(missingTaskId, context.TimeProvider.GetUtcNow().AddMinutes(-2));

        await context.Scheduler.StartAsync();

        Assert.Equal(ReminderStatus.Cancelled, (await context.ReminderRepository.GetByIdAsync(completedReminder.Id))!.Status);
        Assert.Equal(ReminderStatus.Cancelled, (await context.ReminderRepository.GetByIdAsync(missingTaskReminder.Id))!.Status);
        Assert.Empty(context.NotificationService.Attempts);
    }

    [Fact]
    public async Task ResumeAsync_WhenPausedReminderBecomesDue_ShouldCompensateImmediately()
    {
        await using var context = new ReminderSchedulerTestContext();
        var task = context.AddTask();
        var reminder = context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddMinutes(10));
        await context.Scheduler.StartAsync();
        await context.Scheduler.PauseAsync();

        context.TimeProvider.Advance(TimeSpan.FromMinutes(15));
        await context.TimerFactory.Timer!.FireAsync();
        Assert.Equal(ReminderStatus.Pending, (await context.ReminderRepository.GetByIdAsync(reminder.Id))!.Status);
        Assert.Empty(context.NotificationService.Attempts);

        await context.Scheduler.ResumeAsync();

        Assert.Equal(ReminderStatus.Delivered, (await context.ReminderRepository.GetByIdAsync(reminder.Id))!.Status);
        Assert.True(Assert.Single(context.NotificationService.Attempts).IsDelayed);
    }

    [Fact]
    public async Task TimerCallback_WhenNotificationFails_ShouldPersistFailureAndScheduleNextReminder()
    {
        await using var context = new ReminderSchedulerTestContext();
        var task = context.AddTask();
        var failed = context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddMinutes(1));
        context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddMinutes(5));
        context.NotificationService.Result = NotificationDeliveryResult.Failure("notification.denied");
        await context.Scheduler.StartAsync();

        context.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        await context.TimerFactory.Timer!.FireAsync();

        var persisted = await context.ReminderRepository.GetByIdAsync(failed.Id);
        Assert.Equal(ReminderStatus.Failed, persisted!.Status);
        Assert.Equal("notification.denied", persisted.ErrorCode);
        Assert.Equal(TimeSpan.FromMinutes(4), context.TimerFactory.Timer.ScheduledDelay);
    }

    [Fact]
    public async Task TimerCallback_WhenNotificationThrows_ShouldPersistStableFailureAndKeepSchedulerUsable()
    {
        await using var context = new ReminderSchedulerTestContext();
        var task = context.AddTask();
        var reminder = context.AddReminder(task.Id, context.TimeProvider.GetUtcNow().AddMinutes(1));
        context.NotificationService.ExceptionToThrow = new InvalidOperationException("adapter detail");
        await context.Scheduler.StartAsync();

        context.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        await context.TimerFactory.Timer!.FireAsync();

        var persisted = await context.ReminderRepository.GetByIdAsync(reminder.Id);
        Assert.Equal(ReminderStatus.Failed, persisted!.Status);
        Assert.Equal("notification.delivery-failed", persisted.ErrorCode);
        await context.Scheduler.RescheduleAsync();
    }

    private sealed class ReminderSchedulerTestContext : IAsyncDisposable
    {
        private static readonly DateTimeOffset InitialTime = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        private readonly InMemoryTaskStore _taskStore = new();

        public ReminderSchedulerTestContext()
        {
            TaskRepository = new InMemoryTaskRepository(_taskStore);
            ReminderRepository = new InMemoryReminderRepository(_taskStore);
            TimeProvider = new MutableTestTimeProvider(InitialTime);
            TimerFactory = new ManualOneShotTimerFactory();
            NotificationService = new FakeNotificationService();
            Scheduler = new ReminderScheduler(
                ReminderRepository,
                TaskRepository,
                NotificationService,
                TimerFactory,
                TimeProvider,
                NullLogger<ReminderScheduler>.Instance);
        }

        public InMemoryTaskRepository TaskRepository { get; }

        public InMemoryReminderRepository ReminderRepository { get; }

        public MutableTestTimeProvider TimeProvider { get; }

        public ManualOneShotTimerFactory TimerFactory { get; }

        public FakeNotificationService NotificationService { get; }

        public ReminderScheduler Scheduler { get; }

        public TaskItem AddTask(bool completed = false)
        {
            var taskId = Guid.NewGuid();
            var deadlineUtc = TimeProvider.GetUtcNow().AddDays(2);
            var deadlineLocal = deadlineUtc.UtcDateTime;
            var deadline = ZonedDeadline.CreateResolvedUtc(
                DateOnly.FromDateTime(deadlineLocal),
                TimeOnly.FromDateTime(deadlineLocal),
                "UTC",
                deadlineUtc);
            var task = TaskItem.Create(
                taskId,
                "Scheduler task",
                Guid.NewGuid(),
                TaskPriority.Normal,
                TimeProvider.GetUtcNow().AddDays(-2),
                deadline: deadline);
            if (completed)
            {
                task.Complete(TimeProvider.GetUtcNow().AddMinutes(-1));
            }

            _taskStore.Tasks.Add(task.Id, task);
            return task;
        }

        public Reminder AddReminder(Guid taskId, DateTimeOffset scheduledAtUtc)
        {
            var reminder = Reminder.Create(
                Guid.NewGuid(),
                taskId,
                -1440,
                scheduledAtUtc,
                "scheduler-test-" + Guid.NewGuid().ToString("N"));
            if (!_taskStore.Reminders.TryGetValue(taskId, out var reminders))
            {
                reminders = new List<Reminder>();
                _taskStore.Reminders.Add(taskId, reminders);
            }

            reminders.Add(TaskUseCaseTestContext.Clone(reminder));
            return reminder;
        }

        public ValueTask DisposeAsync() => new(Scheduler.StopAsync());
    }

    private sealed class MutableTestTimeProvider : TimeProvider
    {
        private DateTimeOffset _nowUtc;

        public MutableTestTimeProvider(DateTimeOffset nowUtc)
        {
            _nowUtc = nowUtc;
        }

        public override DateTimeOffset GetUtcNow() => _nowUtc;

        public void Advance(TimeSpan delay) => _nowUtc = _nowUtc.Add(delay);
    }

    private sealed class ManualOneShotTimerFactory : IOneShotTimerFactory
    {
        public ManualOneShotTimer? Timer { get; private set; }

        public int CreateCount { get; private set; }

        public IOneShotTimer Create(Func<Task> callback)
        {
            CreateCount++;
            Timer = new ManualOneShotTimer(callback);
            return Timer;
        }
    }

    private sealed class ManualOneShotTimer : IOneShotTimer
    {
        private readonly Func<Task> _callback;

        public ManualOneShotTimer(Func<Task> callback)
        {
            _callback = callback;
        }

        public TimeSpan? ScheduledDelay { get; private set; }

        public int ScheduleCount { get; private set; }

        public void Schedule(TimeSpan delay)
        {
            ScheduleCount++;
            ScheduledDelay = delay;
        }

        public void CancelScheduledCallback() => ScheduledDelay = null;

        public void Dispose() => CancelScheduledCallback();

        public Task FireAsync()
        {
            ScheduledDelay = null;
            return _callback();
        }
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<ReminderNotification> Attempts { get; } = new();

        public bool IsAvailable { get; set; } = true;

        public int CapabilityCheckCount { get; private set; }

        public NotificationDeliveryResult? Result { get; set; }

        public Exception? ExceptionToThrow { get; set; }

        public Task<NotificationProviderCapability> GetCapabilityAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapabilityCheckCount++;
            var result = IsAvailable
                ? NotificationProviderCapability.Available()
                : NotificationProviderCapability.Unavailable("notification.test-unavailable");
            return Task.FromResult(result);
        }

        public Task<NotificationDeliveryResult> ShowAsync(
            ReminderNotification notification,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts.Add(notification);
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result ?? NotificationDeliveryResult.Success());
        }
    }
}
