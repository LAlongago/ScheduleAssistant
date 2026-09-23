using Microsoft.Extensions.Logging.Abstractions;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Reminders;
using ScheduleAssistant.Presentation.Composition;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class ReminderSchedulerHostedServiceTests
{
    [Fact]
    public async Task StartAsync_WhenDatabaseInitializationCompletes_ShouldStartAndRescheduleOnTaskEvents()
    {
        var calls = new List<string>();
        var database = new RecordingDatabaseInitialization(calls);
        var eventBus = new InProcessEventBus();
        var scheduler = new RecordingReminderScheduler(calls);
        var hostedService = new ReminderSchedulerHostedService(
            database,
            eventBus,
            scheduler,
            NullLogger<ReminderSchedulerHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);

        Assert.Equal(2, calls.Count);
        Assert.Equal("database", calls[0]);
        Assert.Equal("scheduler-start", calls[1]);
        await eventBus.PublishAsync(new TaskCreated(Guid.NewGuid(), 1, Array.Empty<DateOnly>()));
        Assert.Equal(1, scheduler.RescheduleCount);

        await hostedService.StopAsync(CancellationToken.None);
        await eventBus.PublishAsync(new TaskUpdated(Guid.NewGuid(), 2, Array.Empty<DateOnly>(), DeadlineChanged: true));

        Assert.Equal(1, scheduler.RescheduleCount);
        Assert.Equal("scheduler-stop", calls[^1]);
    }

    private sealed class RecordingDatabaseInitialization : IDatabaseInitialization
    {
        private readonly List<string> _calls;

        public RecordingDatabaseInitialization(List<string> calls)
        {
            _calls = calls;
        }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            _calls.Add("database");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingReminderScheduler : IReminderScheduler
    {
        private readonly List<string> _calls;

        public RecordingReminderScheduler(List<string> calls)
        {
            _calls = calls;
        }

        public int RescheduleCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _calls.Add("scheduler-start");
            return Task.CompletedTask;
        }

        public Task RescheduleAsync(CancellationToken cancellationToken = default)
        {
            RescheduleCount++;
            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _calls.Add("scheduler-stop");
            return Task.CompletedTask;
        }
    }
}
