namespace ScheduleAssistant.Application.Reminders;

/// <summary>Publishes safe task-open requests raised by a notification provider.</summary>
public interface INotificationActivationSource
{
    /// <summary>Raised when a notification is invoked; null task IDs request only the main window.</summary>
    event EventHandler<NotificationActivationEventArgs>? NotificationActivated;
}

/// <summary>A validated notification activation request without platform-specific payload data.</summary>
public sealed class NotificationActivationEventArgs : EventArgs
{
    /// <summary>Initializes a request for the main window and, when valid, one task.</summary>
    public NotificationActivationEventArgs(Guid? taskId)
    {
        TaskId = taskId;
    }

    /// <summary>Gets the validated task ID, or null when only the main window should open.</summary>
    public Guid? TaskId { get; }
}
