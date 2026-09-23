using ScheduleAssistant.Application.Reminders;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>
/// Explicit DEV-080 fallback until the DEV-081 Windows notification adapter is accepted.
/// It reports an unavailable delivery so the scheduler persists a Failed reminder safely.
/// </summary>
internal sealed class UnavailableNotificationService : INotificationService
{
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
