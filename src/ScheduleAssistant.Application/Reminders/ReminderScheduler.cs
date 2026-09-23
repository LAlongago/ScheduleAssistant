using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ScheduleAssistant.Application.Abstractions.Persistence;

namespace ScheduleAssistant.Application.Reminders;

/// <summary>
/// Drives reminder delivery from persisted state with a single one-shot timer.
/// Startup and resume perform bounded overdue compensation; normal waiting only queries the earliest node.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1848",
    Justification = "Scheduler diagnostics are low-frequency lifecycle and failure events.")]
[SuppressMessage(
    "Performance",
    "CA1873",
    Justification = "Scheduler diagnostics contain only fixed operation labels and stable error codes.")]
[SuppressMessage(
    "Design",
    "CA1001",
    Justification = "This process-wide singleton retains its semaphore until shutdown so pending timer callbacks cannot race with disposal.")]
public sealed partial class ReminderScheduler : IReminderScheduler
{
    private readonly IReminderRepository _reminderRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly INotificationService _notificationService;
    private readonly IOneShotTimerFactory _timerFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReminderScheduler> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IOneShotTimer? _timer;
    private bool _started;
    private bool _paused;
    private bool _stopped;
    private bool _notificationProviderUnavailable;

    /// <summary>Creates a scheduler over the existing task and reminder persistence ports.</summary>
    public ReminderScheduler(
        IReminderRepository reminderRepository,
        ITaskRepository taskRepository,
        INotificationService notificationService,
        IOneShotTimerFactory timerFactory,
        TimeProvider timeProvider,
        ILogger<ReminderScheduler> logger)
    {
        _reminderRepository = reminderRepository ?? throw new ArgumentNullException(nameof(reminderRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_stopped || _started)
            {
                return;
            }

            _started = true;
            _paused = false;
            if (!await EnsureNotificationProviderAvailableAsync("startup", cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (!EnsureTimer())
            {
                return;
            }

            await CompensateAndScheduleAsync("startup", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RescheduleAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_started || _paused || _stopped)
            {
                return;
            }

            var providerWasUnavailable = _notificationProviderUnavailable;
            if (!await EnsureNotificationProviderAvailableAsync("event reschedule", cancellationToken).ConfigureAwait(false)
                || !EnsureTimer())
            {
                return;
            }

            if (providerWasUnavailable)
            {
                await CompensateAndScheduleAsync("notification provider recovery", cancellationToken).ConfigureAwait(false);
                return;
            }

            await TryScheduleNextAsync("event reschedule", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _paused = true;
            _timer?.CancelScheduledCallback();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_stopped)
            {
                return;
            }

            _paused = false;
            _started = true;
            if (!await EnsureNotificationProviderAvailableAsync("resume", cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (!EnsureTimer())
            {
                return;
            }

            await CompensateAndScheduleAsync("resume", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stopped = true;
            _timer?.CancelScheduledCallback();
            _timer?.Dispose();
            _timer = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool EnsureTimer()
    {
        if (_timer is not null)
        {
            return true;
        }

        try
        {
            _timer = _timerFactory.Create(OnTimerElapsedAsync);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError("Reminder scheduler could not create its one-shot timer; exception type {ExceptionType}.", exception.GetType().Name);
            return false;
        }
    }

    private async Task CompensateAndScheduleAsync(string operation, CancellationToken cancellationToken)
    {
        if (!await TryRunAsync(
                operation + " compensation",
                () => CompensateDueRemindersAsync(cancellationToken),
                cancellationToken).ConfigureAwait(false))
        {
            _timer?.CancelScheduledCallback();
            return;
        }

        if (_notificationProviderUnavailable)
        {
            _timer?.CancelScheduledCallback();
            return;
        }

        await TryScheduleNextAsync(operation + " scheduling", cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TryRunAsync(
        string operation,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action().ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError("Reminder scheduler operation {Operation} failed with exception type {ExceptionType}.", operation, exception.GetType().Name);
            return false;
        }
    }
}
