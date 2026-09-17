using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Domain.Tests;

internal static class DomainTestData
{
    public static readonly Guid CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid TaskId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static TaskItem CreateTask(
        DateOnly? plannedDate = null,
        TimeOnly? plannedStart = null,
        TimeOnly? plannedEnd = null,
        ZonedDeadline? deadline = null)
    {
        return TaskItem.Create(
            TaskId,
            "Task",
            CategoryId,
            TaskPriority.Normal,
            CreatedAtUtc,
            plannedDate,
            plannedStart,
            plannedEnd,
            deadline);
    }

    public static TaskItem CreateTaskWith(
        DateOnly? plannedDate = null,
        TimeOnly? plannedStart = null,
        TimeOnly? plannedEnd = null,
        ZonedDeadline? deadline = null,
        string? location = null,
        string? description = null,
        string? materials = null,
        string? notes = null)
    {
        return TaskItem.Create(
            TaskId,
            "Task",
            CategoryId,
            TaskPriority.Normal,
            CreatedAtUtc,
            plannedDate,
            plannedStart,
            plannedEnd,
            deadline,
            location,
            description,
            materials,
            notes);
    }

    public static TaskItem CreateTaskWithTitle(string title)
    {
        return TaskItem.Create(TaskId, title, CategoryId, CreatedAtUtc);
    }

    public static TaskItem RehydrateTask(
        WorkflowStatus workflowStatus = WorkflowStatus.Pending,
        DateTimeOffset? completedAtUtc = null,
        DateTimeOffset? updatedAtUtc = null,
        long version = 1,
        Guid? seriesId = null,
        OccurrenceDate? occurrenceDate = null,
        bool isOccurrenceOverride = false)
    {
        return TaskItem.Rehydrate(
            TaskId,
            "Task",
            CategoryId,
            TaskPriority.Normal,
            workflowStatus,
            CreatedAtUtc,
            updatedAtUtc ?? CreatedAtUtc,
            version,
            seriesId: seriesId,
            occurrenceDate: occurrenceDate,
            isOccurrenceOverride: isOccurrenceOverride,
            completedAtUtc: completedAtUtc);
    }

    public static ZonedDeadline CreateDeadline(DateTimeOffset utc, DateOnly? localDate = null, TimeOnly? localTime = null)
    {
        return new ZonedDeadline(
            localDate ?? new DateOnly(2026, 1, 2),
            localTime ?? new TimeOnly(12, 0),
            "China Standard Time",
            utc);
    }

    public static DateTimeOffset AtUtc(int year, int month, int day, int hour = 0, int minute = 0)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
    }
}
