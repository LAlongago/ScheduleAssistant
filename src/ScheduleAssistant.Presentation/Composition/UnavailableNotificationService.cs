using ScheduleAssistant.Application.Reminders;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>
/// Explicit DEV-080 fallback until the DEV-081 Windows notification adapter is accepted.
/// Its capability result prevents the scheduler from consuming pending reminders.
/// </summary>
internal sealed class UnavailableNotificationService : INotificationService
{
    /// <inheritdoc />
    public Task<NotificationProviderCapability> GetCapabilityAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            NotificationProviderCapability.Unavailable("notification.adapter-not-configured"));
    }

    /// <inheritdoc />
    public Task<NotificationDeliveryResult> ShowAsync(
        ReminderNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(NotificationDeliveryResult.Failure("notification.adapter-not-configured"));
    }
}
