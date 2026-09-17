namespace ScheduleAssistant.Domain;

public sealed partial class TaskItem
{
    private static EditableValues ValidateEditableFields(
        string title,
        Guid categoryId,
        TaskPriority priority,
        DateOnly? plannedDate,
        TimeOnly? plannedStart,
        TimeOnly? plannedEnd,
        ZonedDeadline? deadline,
        string? location,
        string? description,
        string? materials,
        string? notes)
    {
        if (!plannedDate.HasValue && (plannedStart.HasValue || plannedEnd.HasValue))
        {
            throw new DomainValidationException("Planned times require a planned date.", nameof(plannedDate));
        }

        return new EditableValues(
            DomainValidation.NormalizeTitle(title, nameof(title), MaximumTitleLength),
            DomainValidation.RequireNonEmpty(categoryId, nameof(categoryId)),
            DomainValidation.RequireDefinedEnum(priority, nameof(priority)),
            plannedDate,
            new PlannedTimeRange(plannedStart, plannedEnd),
            deadline,
            DomainValidation.NormalizeOptionalText(location, nameof(location), MaximumLocationLength),
            DomainValidation.NormalizeOptionalText(description, nameof(description), MaximumLongTextLength),
            DomainValidation.NormalizeOptionalText(materials, nameof(materials), MaximumLongTextLength),
            DomainValidation.NormalizeOptionalText(notes, nameof(notes), MaximumLongTextLength));
    }

    private static void ValidateOccurrenceIdentity(Guid? seriesId, OccurrenceDate? occurrenceDate, bool isOccurrenceOverride)
    {
        if (seriesId.HasValue)
        {
            DomainValidation.RequireNonEmpty(seriesId.Value, nameof(seriesId));
        }

        if (seriesId.HasValue != (occurrenceDate is not null))
        {
            throw new DomainValidationException("SeriesId and OccurrenceDate must be provided together.");
        }

        if (isOccurrenceOverride && !seriesId.HasValue)
        {
            throw new DomainValidationException("Only a recurrence instance can be an occurrence override.", nameof(isOccurrenceOverride));
        }
    }

    private static void ValidateWorkflowState(WorkflowStatus workflowStatus, DateTimeOffset? completedAtUtc)
    {
        DomainValidation.RequireDefinedEnum(workflowStatus, nameof(workflowStatus));
        if (workflowStatus == WorkflowStatus.Completed && !completedAtUtc.HasValue)
        {
            throw new DomainValidationException("Completed tasks must have a completion timestamp.", nameof(completedAtUtc));
        }

        if (workflowStatus != WorkflowStatus.Completed && completedAtUtc.HasValue)
        {
            throw new DomainValidationException("Non-completed tasks must not have a completion timestamp.", nameof(completedAtUtc));
        }
    }

    private static void ValidateCompletionTimeline(
        DateTimeOffset? completedAtUtc,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        if (!completedAtUtc.HasValue)
        {
            return;
        }

        DomainValidation.EnsureAtOrAfter(completedAtUtc.Value, createdAtUtc, nameof(completedAtUtc));
        DomainValidation.EnsureAtOrAfter(updatedAtUtc, completedAtUtc.Value, nameof(updatedAtUtc));
    }

    private static long ValidateVersion(long version)
    {
        if (version < 1)
        {
            throw new DomainValidationException("Version must be at least one.", nameof(version));
        }

        return version;
    }

    private DateTimeOffset ValidateUpdateTimestamp(DateTimeOffset timestamp)
    {
        var normalized = DomainValidation.NormalizeUtc(timestamp, nameof(timestamp));
        DomainValidation.EnsureAtOrAfter(normalized, CreatedAtUtc, nameof(timestamp));
        DomainValidation.EnsureAtOrAfter(normalized, UpdatedAtUtc, nameof(timestamp));
        return normalized;
    }

    private sealed class EditableValues
    {
        public EditableValues(
            string title,
            Guid categoryId,
            TaskPriority priority,
            DateOnly? plannedDate,
            PlannedTimeRange plannedTime,
            ZonedDeadline? deadline,
            string? location,
            string? description,
            string? materials,
            string? notes)
        {
            Title = title;
            CategoryId = categoryId;
            Priority = priority;
            PlannedDate = plannedDate;
            PlannedTime = plannedTime;
            Deadline = deadline;
            Location = location;
            Description = description;
            Materials = materials;
            Notes = notes;
        }

        public string Title { get; }

        public Guid CategoryId { get; }

        public TaskPriority Priority { get; }

        public DateOnly? PlannedDate { get; }

        public PlannedTimeRange PlannedTime { get; }

        public ZonedDeadline? Deadline { get; }

        public string? Location { get; }

        public string? Description { get; }

        public string? Materials { get; }

        public string? Notes { get; }
    }
}
