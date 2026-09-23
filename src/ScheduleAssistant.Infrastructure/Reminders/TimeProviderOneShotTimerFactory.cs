using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ScheduleAssistant.Application.Reminders;

namespace ScheduleAssistant.Infrastructure.Reminders;

/// <summary>Creates one-shot timers backed by the application's injected <see cref="TimeProvider"/>.</summary>
[SuppressMessage(
    "Performance",
    "CA1848",
    Justification = "This adapter logs only unexpected timer callback failures.")]
[SuppressMessage(
    "Performance",
    "CA1873",
    Justification = "The timer callback diagnostic has a fixed message and no task content.")]
public sealed class TimeProviderOneShotTimerFactory : IOneShotTimerFactory
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TimeProviderOneShotTimerFactory> _logger;

    /// <summary>Initializes the timer factory.</summary>
    public TimeProviderOneShotTimerFactory(
        TimeProvider timeProvider,
        ILogger<TimeProviderOneShotTimerFactory> logger)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IOneShotTimer Create(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return new TimeProviderOneShotTimer(_timeProvider, callback, _logger);
    }

    private sealed class TimeProviderOneShotTimer : IOneShotTimer
    {
        private readonly Func<Task> _callback;
        private readonly ILogger _logger;
        private readonly ITimer _timer;

        public TimeProviderOneShotTimer(
            TimeProvider timeProvider,
            Func<Task> callback,
            ILogger logger)
        {
            _callback = callback;
            _logger = logger;
            _timer = timeProvider.CreateTimer(
                static state => ((TimeProviderOneShotTimer)state!).OnTimerFired(),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
        }

        public void Schedule(TimeSpan delay)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero, nameof(delay));

            _timer.Change(delay, Timeout.InfiniteTimeSpan);
        }

        public void CancelScheduledCallback()
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private void OnTimerFired()
        {
            _ = InvokeCallbackAsync();
        }

        private async Task InvokeCallbackAsync()
        {
            try
            {
                await _callback().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogError("One-shot reminder timer callback failed with exception type {ExceptionType}.", exception.GetType().Name);
            }
        }
    }
}
