namespace ScheduleAssistant.Domain;

/// <summary>
/// Calculates deadline urgency independently from priority and category color.
/// </summary>
public static class DeadlineUrgencyCalculator
{
    /// <summary>
    /// Returns <see cref="DeadlineUrgencyLevel.None"/> for completed tasks or tasks without a deadline.
    /// </summary>
    public static DeadlineUrgencyLevel Calculate(TaskItem task, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.WorkflowStatus == WorkflowStatus.Completed || !task.DeadlineUtc.HasValue)
        {
            return DeadlineUrgencyLevel.None;
        }

        var normalizedNowUtc = DomainValidation.NormalizeUtc(nowUtc, nameof(nowUtc));
        var remaining = task.DeadlineUtc.Value - normalizedNowUtc;

        if (remaining > TimeSpan.FromDays(7))
        {
            return DeadlineUrgencyLevel.Neutral;
        }

        if (remaining > TimeSpan.FromDays(3))
        {
            return DeadlineUrgencyLevel.MoreThanThreeDays;
        }

        if (remaining >= TimeSpan.FromDays(1))
        {
            return DeadlineUrgencyLevel.OneToThreeDays;
        }

        if (remaining >= TimeSpan.Zero)
        {
            return DeadlineUrgencyLevel.LessThanOneDay;
        }

        return DeadlineUrgencyLevel.Overdue;
    }
}
