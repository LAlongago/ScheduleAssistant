namespace ScheduleAssistant.Application.Reminders;

public sealed partial class ReminderScheduler
{
    private static readonly TimeSpan MaximumTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1d);

    private async Task<bool> TryScheduleNextAsync(string operation, CancellationToken cancellationToken)
    {
        var scheduled = await TryRunAsync(
            operation,
            () => ScheduleNextCoreAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);
        if (!scheduled)
        {
            _timer?.CancelScheduledCallback();
        }

        return scheduled;
    }

    private async Task ScheduleNextCoreAsync(CancellationToken cancellationToken)
    {
        var nextReminder = await _reminderRepository
            .GetNextPendingAsync(cancellationToken)
            .ConfigureAwait(false);
        if (nextReminder is null)
        {
            _timer?.CancelScheduledCallback();
            return;
        }

        var delay = nextReminder.ScheduledAtUtc - _timeProvider.GetUtcNow().ToUniversalTime();
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        _timer?.Schedule(delay > MaximumTimerDelay ? MaximumTimerDelay : delay);
    }

    private async Task OnTimerElapsedAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_started || _paused || _stopped)
            {
                return;
            }

            if (!await TryRunAsync(
                    "timer delivery",
                    () => ProcessNextPendingReminderAsync(CancellationToken.None),
                    CancellationToken.None).ConfigureAwait(false))
            {
                _timer?.CancelScheduledCallback();
                return;
            }

            await TryScheduleNextAsync("timer reschedule", CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
