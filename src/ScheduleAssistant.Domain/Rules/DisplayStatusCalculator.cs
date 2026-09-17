namespace ScheduleAssistant.Domain;

/// <summary>
/// Calculates the derived display status using the specification's fixed precedence order.
/// </summary>
public static class DisplayStatusCalculator
{
    /// <summary>
    /// Calculates status from explicit UTC now and explicit user-local today.
    /// </summary>
    public static DisplayStatus Calculate(TaskItem task, DateTimeOffset nowUtc, DateOnly todayLocal)
    {
        ArgumentNullException.ThrowIfNull(task);
        var normalizedNowUtc = DomainValidation.NormalizeUtc(nowUtc, nameof(nowUtc));

        if (task.WorkflowStatus == WorkflowStatus.Completed)
        {
            return DisplayStatus.Completed;
        }

        if (task.DeadlineUtc.HasValue && task.DeadlineUtc.Value < normalizedNowUtc)
        {
            return DisplayStatus.Overdue;
        }

        if (task.WorkflowStatus == WorkflowStatus.InProgress)
        {
            return DisplayStatus.InProgress;
        }

        if (task.PlannedDate.HasValue && task.PlannedDate.Value < todayLocal)
        {
            return DisplayStatus.PlannedPast;
        }

        return DisplayStatus.NotStarted;
    }
}
