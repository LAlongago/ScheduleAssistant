namespace ScheduleAssistant.Application.Abstractions.Events;

/// <summary>Base contract for lightweight in-process application events.</summary>
public interface IApplicationEvent
{
    /// <summary>Gets the task affected by the event.</summary>
    Guid TaskId { get; }

    /// <summary>Gets the persisted task version associated with the event.</summary>
    long Version { get; }

    /// <summary>Gets dates whose task projections may need a local refresh.</summary>
    IReadOnlyList<DateOnly> AffectedDates { get; }
}

/// <summary>Publishes application events after a successful persistence commit.</summary>
public interface IApplicationEventPublisher
{
    /// <summary>Publishes one event to in-process subscribers.</summary>
    Task PublishAsync(IApplicationEvent applicationEvent, CancellationToken cancellationToken = default);
}

/// <summary>Base data shared by task-local refresh events.</summary>
public abstract record TaskApplicationEvent(
    Guid TaskId,
    long Version,
    IReadOnlyList<DateOnly> AffectedDates) : IApplicationEvent;

/// <summary>Raised after a task has been created.</summary>
public sealed record TaskCreated(
    Guid TaskId,
    long Version,
    IReadOnlyList<DateOnly> AffectedDates) : TaskApplicationEvent(TaskId, Version, AffectedDates);

/// <summary>Raised after editable task details have been committed.</summary>
public sealed record TaskUpdated(
    Guid TaskId,
    long Version,
    IReadOnlyList<DateOnly> AffectedDates,
    bool DeadlineChanged) : TaskApplicationEvent(TaskId, Version, AffectedDates);

/// <summary>Raised after a task changes between completed and not completed.</summary>
public sealed record TaskCompletedChanged(
    Guid TaskId,
    long Version,
    IReadOnlyList<DateOnly> AffectedDates,
    bool IsCompleted) : TaskApplicationEvent(TaskId, Version, AffectedDates);

/// <summary>Raised after a task has been deleted.</summary>
public sealed record TaskDeleted(
    Guid TaskId,
    long Version,
    IReadOnlyList<DateOnly> AffectedDates) : TaskApplicationEvent(TaskId, Version, AffectedDates);

/// <summary>Raised when the persisted reminder plan for a task may have changed.</summary>
public sealed record ReminderPlanChanged(
    Guid TaskId,
    long Version,
    IReadOnlyList<DateOnly> AffectedDates,
    bool HasPendingPlan) : TaskApplicationEvent(TaskId, Version, AffectedDates);

/// <summary>
/// A small synchronous-in-process event bus. Subscribers are called in registration order;
/// a subscriber failure is allowed to reach the committing use case so it can return a refresh signal.
/// </summary>
public sealed class InProcessEventBus : IApplicationEventPublisher
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    /// <summary>Subscribes to one event type and returns a removable registration.</summary>
    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IApplicationEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        Func<IApplicationEvent, CancellationToken, Task> adapter =
            (applicationEvent, cancellationToken) => handler((TEvent)applicationEvent, cancellationToken);
        lock (_gate)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers = new List<Delegate>();
                _handlers.Add(typeof(TEvent), handlers);
            }

            handlers.Add(adapter);
        }

        return new Subscription(() => Remove(typeof(TEvent), adapter));
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        IApplicationEvent applicationEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);
        Delegate[] handlers;
        lock (_gate)
        {
            handlers = _handlers
                .Where(pair => pair.Key.IsAssignableFrom(applicationEvent.GetType()))
                .SelectMany(pair => pair.Value)
                .ToArray();
        }

        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await InvokeAsync(handler, applicationEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task InvokeAsync(
        Delegate handler,
        IApplicationEvent applicationEvent,
        CancellationToken cancellationToken)
    {
        return ((Func<IApplicationEvent, CancellationToken, Task>)handler)(applicationEvent, cancellationToken);
    }

    private void Remove(Type eventType, Delegate handler)
    {
        lock (_gate)
        {
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                return;
            }

            handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                _handlers.Remove(eventType);
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _dispose;
        private int _disposed;

        public Subscription(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _dispose();
            }
        }
    }
}
