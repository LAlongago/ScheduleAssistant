using Microsoft.Extensions.Logging;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Reminders;

public sealed partial class ReminderScheduler
{
    private static readonly TimeSpan CompensationWindow = TimeSpan.FromHours(24);
    private const string NotificationExceptionCode = "notification.delivery-failed";

    private async Task CompensateDueRemindersAsync(CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        var dueReminders = await _reminderRepository
            .GetPendingDueAsync(nowUtc, cancellationToken)
            .ConfigureAwait(false);

        foreach (var reminder in dueReminders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessReminderAsync(reminder.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessNextPendingReminderAsync(CancellationToken cancellationToken)
    {
        var reminder = await _reminderRepository.GetNextPendingAsync(cancellationToken).ConfigureAwait(false);
        if (reminder is null || reminder.ScheduledAtUtc > _timeProvider.GetUtcNow().ToUniversalTime())
        {
            return;
        }

        await ProcessReminderAsync(reminder.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessReminderAsync(Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await _reminderRepository.GetByIdAsync(reminderId, cancellationToken).ConfigureAwait(false);
        if (reminder is null || reminder.Status != ReminderStatus.Pending)
        {
            return;
        }

        var task = await _taskRepository.GetByIdAsync(reminder.TaskId, cancellationToken).ConfigureAwait(false);
        if (task is null || task.WorkflowStatus == WorkflowStatus.Completed || !task.DeadlineUtc.HasValue)
        {
            reminder.Cancel();
            await _reminderRepository.UpdateAsync(reminder, cancellationToken).ConfigureAwait(false);
            return;
        }

        var nowUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        if (reminder.ScheduledAtUtc < nowUtc - CompensationWindow)
        {
            reminder.MarkExpired();
            await _reminderRepository.UpdateAsync(reminder, cancellationToken).ConfigureAwait(false);
            return;
        }

        NotificationDeliveryResult delivery;
        try
        {
            delivery = await _notificationService
                .ShowAsync(
                    new ReminderNotification(
                        task.Id,
                        task.Title,
                        task.DeadlineUtc.Value,
                        reminder.DeduplicationKey,
                        IsDelayed: reminder.ScheduledAtUtc < nowUtc,
                        IsDeadlineOverdue: task.DeadlineUtc.Value <= nowUtc),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError("Reminder notification failed for reminder {ReminderId}; exception type {ExceptionType}.", reminder.Id, exception.GetType().Name);
            delivery = NotificationDeliveryResult.Failure(NotificationExceptionCode);
        }

        if (delivery.IsSuccess)
        {
            reminder.MarkDelivered(_timeProvider.GetUtcNow().ToUniversalTime());
        }
        else
        {
            var errorCode = delivery.ErrorCode ?? NotificationExceptionCode;
            reminder.MarkFailed(errorCode);
            _logger.LogWarning(
                "Reminder notification was not delivered for reminder {ReminderId}; error code {ErrorCode}.",
                reminder.Id,
                errorCode);
        }

        await _reminderRepository.UpdateAsync(reminder, cancellationToken).ConfigureAwait(false);
    }
}
