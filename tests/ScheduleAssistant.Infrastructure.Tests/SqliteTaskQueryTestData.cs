using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Tests;

internal static class SqliteTaskQueryTestData
{
    public static TaskItem CreateTask(
        Guid categoryId,
        string title,
        DateTimeOffset createdAtUtc,
        Guid? id = null,
        TaskPriority priority = TaskPriority.Normal,
        WorkflowStatus workflowStatus = WorkflowStatus.Pending,
        DateOnly? plannedDate = null,
        TimeOnly? plannedStart = null,
        ZonedDeadline? deadline = null,
        string? location = null,
        string? description = null,
        string? materials = null,
        string? notes = null)
    {
        var task = TaskItem.Create(
            id ?? Guid.NewGuid(),
            title,
            categoryId,
            priority,
            createdAtUtc,
            plannedDate,
            plannedStart,
            plannedStart?.AddHours(1),
            deadline,
            location,
            description,
            materials,
            notes);

        if (workflowStatus == WorkflowStatus.InProgress)
        {
            task.StartProcessing(createdAtUtc.AddMinutes(1));
        }
        else if (workflowStatus == WorkflowStatus.Completed)
        {
            task.Complete(createdAtUtc.AddMinutes(1));
        }

        return task;
    }

    public static ZonedDeadline CreateDeadline(
        DateOnly localDate,
        TimeOnly localTime,
        DateTimeOffset utc)
    {
        return ZonedDeadline.CreateResolvedUtc(localDate, localTime, "China Standard Time", utc);
    }
}
