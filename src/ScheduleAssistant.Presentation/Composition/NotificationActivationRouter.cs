using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ScheduleAssistant.Application.Reminders;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Queues cold-start activation until the shell is visible, then routes validated task IDs on the UI dispatcher.</summary>
[SuppressMessage(
    "Design",
    "CA1031",
    Justification = "Notification activation is an external OS callback; malformed or stale task state must leave the main shell usable.")]
[SuppressMessage(
    "Performance",
    "CA1848",
    Justification = "Diagnostics occur only when a notification activation cannot be routed.")]
public sealed class NotificationActivationRouter
{
    private readonly object _gate = new();
    private readonly Queue<NotificationActivationEventArgs> _pending = new();
    private readonly IUiDispatcher _dispatcher;
    private readonly IWindowService _windowService;
    private readonly ITaskEditorService _taskEditorService;
    private readonly ILogger<NotificationActivationRouter> _logger;
    private bool _mainWindowReady;

    /// <summary>Creates the router used by both in-process notification clicks and cold-start activations.</summary>
    public NotificationActivationRouter(
        IUiDispatcher dispatcher,
        IWindowService windowService,
        ITaskEditorService taskEditorService,
        ILogger<NotificationActivationRouter> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _taskEditorService = taskEditorService ?? throw new ArgumentNullException(nameof(taskEditorService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Marks the shell ready and drains activation requests received during host startup.</summary>
    public Task AttachMainWindowAsync()
    {
        NotificationActivationEventArgs[] pending;
        lock (_gate)
        {
            _mainWindowReady = true;
            pending = _pending.ToArray();
            _pending.Clear();
        }

        return Task.WhenAll(pending.Select(RouteSafelyAsync));
    }

    /// <summary>Routes an activation request immediately or retains it until the main window is ready.</summary>
    public Task HandleActivationAsync(NotificationActivationEventArgs request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (!_mainWindowReady)
            {
                _pending.Enqueue(request);
                return Task.CompletedTask;
            }
        }

        return RouteSafelyAsync(request);
    }

    private async Task RouteSafelyAsync(NotificationActivationEventArgs request)
    {
        try
        {
            await _dispatcher.InvokeAsync(async () =>
            {
                _windowService.ActivateMainWindow();
                if (request.TaskId is Guid taskId)
                {
                    var opened = await _taskEditorService
                        .TryOpenEditAsync(taskId, CancellationToken.None);
                    if (!opened)
                    {
                        _logger.LogInformation(
                            "Notification activation task was unavailable; task {TaskId}, status {Status}.",
                            taskId,
                            "not-found-or-unavailable");
                    }
                }
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Notification activation routing failed for task {TaskId}; exception type {ExceptionType}, HRESULT {HResult}.",
                request.TaskId,
                exception.GetType().Name,
                exception.HResult);
        }
    }
}
