using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Reminders;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Starts reminder scheduling after database initialization and reschedules on task events.</summary>
[SuppressMessage(
    "Performance",
    "CA1848",
    Justification = "Scheduler host diagnostics are emitted only on event-handler failures.")]
[SuppressMessage(
    "Performance",
    "CA1873",
    Justification = "The event-handler diagnostic contains a fixed operation and event type only.")]
public sealed class ReminderSchedulerHostedService : IHostedService
{
    private readonly IDatabaseInitialization _databaseInitialization;
    private readonly InProcessEventBus _eventBus;
    private readonly IReminderScheduler _scheduler;
    private readonly ILogger<ReminderSchedulerHostedService> _logger;
    private IDisposable? _eventSubscription;

    /// <summary>Initializes the hosted scheduler and its event subscription boundary.</summary>
    public ReminderSchedulerHostedService(
        IDatabaseInitialization databaseInitialization,
        InProcessEventBus eventBus,
        IReminderScheduler scheduler,
        ILogger<ReminderSchedulerHostedService> logger)
    {
        _databaseInitialization = databaseInitialization
            ?? throw new ArgumentNullException(nameof(databaseInitialization));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _databaseInitialization.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        _eventSubscription = _eventBus.Subscribe<TaskApplicationEvent>(HandleTaskEventAsync);
        try
        {
            await _scheduler.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _eventSubscription.Dispose();
            _eventSubscription = null;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _eventSubscription?.Dispose();
        _eventSubscription = null;
        await _scheduler.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleTaskEventAsync(
        TaskApplicationEvent applicationEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            await _scheduler.RescheduleAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Reminder rescheduling failed after application event {EventType}; exception type {ExceptionType}.",
                applicationEvent.GetType().Name,
                exception.GetType().Name);
        }
    }
}
