using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Prevents two ScheduleAssistant business windows in the same Windows user session.</summary>
[SuppressMessage(
    "Design",
    "CA1031",
    Justification = "The secondary-launch signal must not prevent normal application startup or shutdown if the primary exits concurrently.")]
public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = "Local\\ScheduleAssistant.BusinessInstance";
    private const string ActivationEventName = "Local\\ScheduleAssistant.BusinessInstance.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _activationSignal;
    private readonly CancellationTokenSource _stop = new();
    private Task? _listenerTask;
    private bool _ownsMutex;
    private bool _disposed;

    /// <summary>Creates or joins the per-session single-instance coordination boundary.</summary>
    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            _ownsMutex = _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        if (_ownsMutex)
        {
            _activationSignal = new EventWaitHandle(
                initialState: false,
                EventResetMode.AutoReset,
                ActivationEventName);
        }
        else
        {
            try
            {
                _activationSignal = EventWaitHandle.OpenExisting(ActivationEventName);
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                _activationSignal = null;
            }
        }
    }

    /// <summary>Gets whether this process owns the business instance mutex.</summary>
    public bool IsPrimary => _ownsMutex;

    /// <summary>Starts the primary listener for secondary launches.</summary>
    public void StartListening(Action onActivationRequested)
    {
        ArgumentNullException.ThrowIfNull(onActivationRequested);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_ownsMutex || _activationSignal is null || _listenerTask is not null)
        {
            return;
        }

        _listenerTask = Task.Run(() => Listen(onActivationRequested));
    }

    /// <summary>Signals the primary process, if one is available, to restore its main window.</summary>
    public void SignalPrimary()
    {
        if (_ownsMutex)
        {
            return;
        }

        try
        {
            _activationSignal?.Set();
        }
        catch (ObjectDisposedException)
        {
            // The primary may exit while a secondary launch is being forwarded.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stop.Cancel();
        if (_listenerTask is not null)
        {
            try
            {
                _listenerTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Normal application shutdown.
            }
        }

        _activationSignal?.Dispose();
        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Another owner state is already active after an interrupted process exit.
            }
        }

        _mutex.Dispose();
        _stop.Dispose();
    }

    private void Listen(Action onActivationRequested)
    {
        if (_activationSignal is null)
        {
            return;
        }

        var waitHandles = new WaitHandle[] { _activationSignal, _stop.Token.WaitHandle };
        while (WaitHandle.WaitAny(waitHandles) == 0)
        {
            try
            {
                onActivationRequested();
            }
            catch
            {
                // The listener must remain available even if a UI activation callback fails.
            }
        }
    }
}
