using System.Windows.Threading;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Marshals asynchronous page updates back to the WPF UI dispatcher.</summary>
public interface IUiDispatcher
{
    /// <summary>Gets whether the caller is already running on the UI dispatcher.</summary>
    bool CheckAccess { get; }

    /// <summary>Runs a UI-bound action on the UI dispatcher.</summary>
    Task InvokeAsync(Action action);
}

/// <summary>WPF implementation of the presentation dispatcher boundary.</summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    /// <summary>Initializes the dispatcher wrapper.</summary>
    public WpfUiDispatcher(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public bool CheckAccess => _dispatcher.CheckAccess();

    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _dispatcher.CheckAccess()
            ? RunOnCurrentDispatcher(action)
            : _dispatcher.InvokeAsync(action, DispatcherPriority.DataBind).Task;
    }

    private static Task RunOnCurrentDispatcher(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Coordinates the cached per-minute countdown tick and the one-shot deadline refresh tick.
/// </summary>
public interface IDeadlineRefreshTimer : IDisposable
{
    /// <summary>Starts the timer callbacks.</summary>
    void Start(Func<Task> onMinuteTick, Func<Task> onDeadlineReached);

    /// <summary>Schedules a single callback for the next future deadline.</summary>
    void ScheduleDeadline(DateTimeOffset? deadlineUtc);
}

/// <summary>TimeProvider-backed timer used by the Today page.</summary>
public sealed class DeadlineRefreshTimer : IDeadlineRefreshTimer
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private ITimer? _minuteTimer;
    private ITimer? _deadlineTimer;
    private Func<Task>? _onMinuteTick;
    private Func<Task>? _onDeadlineReached;
    private bool _disposed;

    /// <summary>Initializes the timer with an injectable clock.</summary>
    public DeadlineRefreshTimer(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public void Start(Func<Task> onMinuteTick, Func<Task> onDeadlineReached)
    {
        ArgumentNullException.ThrowIfNull(onMinuteTick);
        ArgumentNullException.ThrowIfNull(onDeadlineReached);

        lock (_gate)
        {
            ThrowIfDisposed();
            _onMinuteTick = onMinuteTick;
            _onDeadlineReached = onDeadlineReached;
            _minuteTimer?.Dispose();
            _minuteTimer = _timeProvider.CreateTimer(
                static state => ((DeadlineRefreshTimer)state!).FireMinuteTick(),
                this,
                Minute,
                Minute);
        }
    }

    /// <inheritdoc />
    public void ScheduleDeadline(DateTimeOffset? deadlineUtc)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _deadlineTimer?.Dispose();
            _deadlineTimer = null;
            if (!deadlineUtc.HasValue)
            {
                return;
            }

            var nowUtc = _timeProvider.GetUtcNow().ToUniversalTime();
            var dueIn = deadlineUtc.Value.ToUniversalTime() - nowUtc;
            if (dueIn < TimeSpan.Zero)
            {
                dueIn = TimeSpan.Zero;
            }

            _deadlineTimer = _timeProvider.CreateTimer(
                static state => ((DeadlineRefreshTimer)state!).FireDeadlineReached(),
                this,
                dueIn,
                Timeout.InfiniteTimeSpan);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _minuteTimer?.Dispose();
            _deadlineTimer?.Dispose();
            _minuteTimer = null;
            _deadlineTimer = null;
            _onMinuteTick = null;
            _onDeadlineReached = null;
        }
    }

    private void FireMinuteTick()
    {
        Func<Task>? callback;
        lock (_gate)
        {
            callback = _disposed ? null : _onMinuteTick;
        }

        FireAsync(callback);
    }

    private void FireDeadlineReached()
    {
        Func<Task>? callback;
        lock (_gate)
        {
            callback = _disposed ? null : _onDeadlineReached;
        }

        FireAsync(callback);
    }

    private static void FireAsync(Func<Task>? callback)
    {
        if (callback is null)
        {
            return;
        }

        _ = InvokeSafelyAsync(callback);
    }

    private static async Task InvokeSafelyAsync(Func<Task> callback)
    {
        try
        {
            await callback().ConfigureAwait(false);
        }
        catch
        {
            // Page ViewModels convert query failures to visible state. Timer callbacks
            // must never surface an unhandled exception on the timer thread.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
