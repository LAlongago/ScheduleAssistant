namespace ScheduleAssistant.Application.Reminders;

/// <summary>
/// A reminder notification payload independent of any operating-system notification API.
/// The deduplication key lets platform adapters suppress duplicate delivery after process recovery.
/// </summary>
public sealed record ReminderNotification(
    Guid TaskId,
    string TaskTitle,
    DateTimeOffset DeadlineUtc,
    string DeduplicationKey,
    bool IsDelayed,
    bool IsDeadlineOverdue);

/// <summary>Result of asking a notification adapter to show a reminder.</summary>
public sealed record NotificationDeliveryResult
{
    private NotificationDeliveryResult(bool isSuccess, string? errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    /// <summary>Gets whether the notification adapter accepted the delivery.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a stable diagnostic code when delivery failed.</summary>
    public string? ErrorCode { get; }

    /// <summary>Creates a successful delivery result.</summary>
    public static NotificationDeliveryResult Success() => new(true, null);

    /// <summary>Creates a failure result with a stable, non-empty diagnostic code.</summary>
    public static NotificationDeliveryResult Failure(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        var normalized = errorCode.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(errorCode), "Notification error codes are limited to 200 characters.");
        }

        return new NotificationDeliveryResult(false, normalized);
    }
}

/// <summary>Shows a task reminder through a replaceable notification adapter.</summary>
public interface INotificationService
{
    /// <summary>Shows one notification and returns a provider-neutral delivery result.</summary>
    Task<NotificationDeliveryResult> ShowAsync(
        ReminderNotification notification,
        CancellationToken cancellationToken = default);
}
