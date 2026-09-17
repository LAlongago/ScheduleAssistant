namespace ScheduleAssistant.Domain;

/// <summary>
/// Metadata for one reminder node belonging to a task.
/// </summary>
public sealed class Reminder
{
    private Reminder(
        Guid id,
        Guid taskId,
        int relativeOffsetMinutes,
        DateTimeOffset scheduledAtUtc,
        DateTimeOffset? deliveredAtUtc,
        ReminderStatus status,
        string deduplicationKey,
        string? errorCode)
    {
        Id = DomainValidation.RequireNonEmpty(id, nameof(id));
        TaskId = DomainValidation.RequireNonEmpty(taskId, nameof(taskId));
        RelativeOffsetMinutes = relativeOffsetMinutes;
        ScheduledAtUtc = DomainValidation.NormalizeUtc(scheduledAtUtc, nameof(scheduledAtUtc));
        DeduplicationKey = DomainValidation.NormalizeName(deduplicationKey, nameof(deduplicationKey), 200);
        Status = DomainValidation.RequireDefinedEnum(status, nameof(status));
        DeliveredAtUtc = deliveredAtUtc.HasValue
            ? DomainValidation.NormalizeUtc(deliveredAtUtc.Value, nameof(deliveredAtUtc))
            : null;
        ErrorCode = DomainValidation.NormalizeMetadata(errorCode, nameof(errorCode), 200);
        ValidateState();
    }

    /// <summary>
    /// Creates a pending reminder. Negative offsets mean before the task deadline; positive offsets mean after it.
    /// </summary>
    public static Reminder Create(
        Guid id,
        Guid taskId,
        int relativeOffsetMinutes,
        DateTimeOffset scheduledAtUtc,
        string deduplicationKey)
    {
        return new Reminder(
            id,
            taskId,
            relativeOffsetMinutes,
            scheduledAtUtc,
            deliveredAtUtc: null,
            ReminderStatus.Pending,
            deduplicationKey,
            errorCode: null);
    }

    /// <summary>
    /// Rebuilds reminder metadata from persistence without sending or scheduling anything.
    /// </summary>
    public static Reminder Rehydrate(
        Guid id,
        Guid taskId,
        int relativeOffsetMinutes,
        DateTimeOffset scheduledAtUtc,
        DateTimeOffset? deliveredAtUtc,
        ReminderStatus status,
        string deduplicationKey,
        string? errorCode)
    {
        return new Reminder(
            id,
            taskId,
            relativeOffsetMinutes,
            scheduledAtUtc,
            deliveredAtUtc,
            status,
            deduplicationKey,
            errorCode);
    }

    /// <summary>
    /// Gets the immutable reminder identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the immutable owning task identity.
    /// </summary>
    public Guid TaskId { get; }

    /// <summary>
    /// Gets the offset in minutes relative to the task deadline. Negative means before the deadline.
    /// </summary>
    public int RelativeOffsetMinutes { get; private set; }

    /// <summary>
    /// Gets the caller-resolved UTC trigger time.
    /// </summary>
    public DateTimeOffset ScheduledAtUtc { get; private set; }

    /// <summary>
    /// Gets the actual delivery timestamp, if delivered.
    /// </summary>
    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    /// <summary>
    /// Gets the reminder delivery state.
    /// </summary>
    public ReminderStatus Status { get; private set; }

    /// <summary>
    /// Gets the caller-provided key used for deduplication.
    /// </summary>
    public string DeduplicationKey { get; }

    /// <summary>
    /// Gets a diagnostic error code for a failed reminder, if any.
    /// </summary>
    public string? ErrorCode { get; private set; }

    /// <summary>
    /// Reschedules a pending reminder without performing timer or notification work.
    /// </summary>
    public void Reschedule(int relativeOffsetMinutes, DateTimeOffset scheduledAtUtc)
    {
        EnsurePending();
        var normalizedScheduledAt = DomainValidation.NormalizeUtc(scheduledAtUtc, nameof(scheduledAtUtc));
        RelativeOffsetMinutes = relativeOffsetMinutes;
        ScheduledAtUtc = normalizedScheduledAt;
    }

    /// <summary>
    /// Marks a pending reminder as delivered. Repeating delivery is an idempotent no-op.
    /// </summary>
    public void MarkDelivered(DateTimeOffset deliveredAtUtc)
    {
        if (Status == ReminderStatus.Delivered)
        {
            return;
        }

        EnsurePending();
        var normalizedDeliveredAt = DomainValidation.NormalizeUtc(deliveredAtUtc, nameof(deliveredAtUtc));
        Status = ReminderStatus.Delivered;
        DeliveredAtUtc = normalizedDeliveredAt;
        ErrorCode = null;
    }

    /// <summary>
    /// Marks a pending reminder as expired without delivering a notification.
    /// </summary>
    public void MarkExpired()
    {
        if (Status == ReminderStatus.Expired)
        {
            return;
        }

        EnsurePending();
        Status = ReminderStatus.Expired;
        DeliveredAtUtc = null;
        ErrorCode = null;
    }

    /// <summary>
    /// Cancels a pending reminder. Repeating cancellation is an idempotent no-op.
    /// </summary>
    public void Cancel()
    {
        if (Status == ReminderStatus.Cancelled)
        {
            return;
        }

        EnsurePending();
        Status = ReminderStatus.Cancelled;
        DeliveredAtUtc = null;
        ErrorCode = null;
    }

    /// <summary>
    /// Records a delivery failure without invoking a notification service or retry loop.
    /// </summary>
    public void MarkFailed(string errorCode)
    {
        EnsurePending();
        var normalizedErrorCode = DomainValidation.NormalizeName(errorCode, nameof(errorCode), 200);
        Status = ReminderStatus.Failed;
        DeliveredAtUtc = null;
        ErrorCode = normalizedErrorCode;
    }

    private void EnsurePending()
    {
        if (Status != ReminderStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending reminder can change delivery state.");
        }
    }

    private void ValidateState()
    {
        switch (Status)
        {
            case ReminderStatus.Pending:
            case ReminderStatus.Expired:
            case ReminderStatus.Cancelled:
                if (DeliveredAtUtc.HasValue || ErrorCode is not null)
                {
                    throw new DomainValidationException("This reminder status cannot have delivery or error metadata.");
                }

                break;
            case ReminderStatus.Delivered:
                if (!DeliveredAtUtc.HasValue || ErrorCode is not null)
                {
                    throw new DomainValidationException("A delivered reminder must have delivery time and no error code.");
                }

                break;
            case ReminderStatus.Failed:
                if (DeliveredAtUtc.HasValue || ErrorCode is null)
                {
                    throw new DomainValidationException("A failed reminder must have an error code and no delivery time.");
                }

                break;
            default:
                throw new DomainValidationException("Unsupported reminder status.", nameof(Status));
        }
    }
}
