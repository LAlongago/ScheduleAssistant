namespace ScheduleAssistant.Domain;

/// <summary>
/// A normal task or an already-materialized recurrence instance.
/// </summary>
public sealed partial class TaskItem
{
    /// <summary>
    /// Maximum title length in Unicode scalar values.
    /// </summary>
    public const int MaximumTitleLength = 200;

    /// <summary>
    /// Maximum location length in Unicode scalar values.
    /// </summary>
    public const int MaximumLocationLength = 300;

    /// <summary>
    /// Maximum length for the three long text fields in Unicode scalar values.
    /// </summary>
    public const int MaximumLongTextLength = 10_000;

    private TaskItem(
        Guid id,
        string title,
        Guid categoryId,
        TaskPriority priority,
        WorkflowStatus workflowStatus,
        DateOnly? plannedDate,
        TimeOnly? plannedStart,
        TimeOnly? plannedEnd,
        ZonedDeadline? deadline,
        string? location,
        string? description,
        string? materials,
        string? notes,
        Guid? seriesId,
        OccurrenceDate? occurrenceDate,
        bool isOccurrenceOverride,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? completedAtUtc,
        long version)
    {
        Id = DomainValidation.RequireNonEmpty(id, nameof(id));
        var values = ValidateEditableFields(
            title,
            categoryId,
            priority,
            plannedDate,
            plannedStart,
            plannedEnd,
            deadline,
            location,
            description,
            materials,
            notes);

        ValidateOccurrenceIdentity(seriesId, occurrenceDate, isOccurrenceOverride);

        var normalizedCreatedAt = DomainValidation.NormalizeUtc(createdAtUtc, nameof(createdAtUtc));
        var normalizedUpdatedAt = DomainValidation.NormalizeUtc(updatedAtUtc, nameof(updatedAtUtc));
        DomainValidation.EnsureAtOrAfter(normalizedUpdatedAt, normalizedCreatedAt, nameof(updatedAtUtc));
        ValidateWorkflowState(workflowStatus, completedAtUtc);

        Title = values.Title;
        CategoryId = values.CategoryId;
        Priority = values.Priority;
        WorkflowStatus = workflowStatus;
        PlannedDate = values.PlannedDate;
        PlannedTime = values.PlannedTime;
        Deadline = values.Deadline;
        Location = values.Location;
        Description = values.Description;
        Materials = values.Materials;
        Notes = values.Notes;
        SeriesId = seriesId;
        OccurrenceDate = occurrenceDate;
        IsOccurrenceOverride = isOccurrenceOverride;
        CreatedAtUtc = normalizedCreatedAt;
        UpdatedAtUtc = normalizedUpdatedAt;
        CompletedAtUtc = completedAtUtc.HasValue
            ? DomainValidation.NormalizeUtc(completedAtUtc.Value, nameof(completedAtUtc))
            : null;
        Version = ValidateVersion(version);
    }

    /// <summary>
    /// Creates a new pending task with version one and matching creation/update timestamps.
    /// </summary>
    public static TaskItem Create(
        Guid id,
        string title,
        Guid categoryId,
        TaskPriority priority,
        DateTimeOffset createdAtUtc,
        DateOnly? plannedDate = null,
        TimeOnly? plannedStart = null,
        TimeOnly? plannedEnd = null,
        ZonedDeadline? deadline = null,
        string? location = null,
        string? description = null,
        string? materials = null,
        string? notes = null,
        Guid? seriesId = null,
        OccurrenceDate? occurrenceDate = null,
        bool isOccurrenceOverride = false)
    {
        return new TaskItem(
            id,
            title,
            categoryId,
            priority,
            WorkflowStatus.Pending,
            plannedDate,
            plannedStart,
            plannedEnd,
            deadline,
            location,
            description,
            materials,
            notes,
            seriesId,
            occurrenceDate,
            isOccurrenceOverride,
            createdAtUtc,
            createdAtUtc,
            completedAtUtc: null,
            version: 1);
    }

    /// <summary>
    /// Creates a new pending task using normal priority when no priority was supplied by the caller.
    /// </summary>
    public static TaskItem Create(
        Guid id,
        string title,
        Guid categoryId,
        DateTimeOffset createdAtUtc,
        DateOnly? plannedDate = null,
        TimeOnly? plannedStart = null,
        TimeOnly? plannedEnd = null,
        ZonedDeadline? deadline = null,
        string? location = null,
        string? description = null,
        string? materials = null,
        string? notes = null,
        Guid? seriesId = null,
        OccurrenceDate? occurrenceDate = null,
        bool isOccurrenceOverride = false)
    {
        return Create(
            id,
            title,
            categoryId,
            TaskPriority.Normal,
            createdAtUtc,
            plannedDate,
            plannedStart,
            plannedEnd,
            deadline,
            location,
            description,
            materials,
            notes,
            seriesId,
            occurrenceDate,
            isOccurrenceOverride);
    }

    /// <summary>
    /// Rebuilds a task from persistence without replaying creation or workflow behavior.
    /// All persisted values are validated before an object is returned.
    /// </summary>
    public static TaskItem Rehydrate(
        Guid id,
        string title,
        Guid categoryId,
        TaskPriority priority,
        WorkflowStatus workflowStatus,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        long version,
        DateOnly? plannedDate = null,
        TimeOnly? plannedStart = null,
        TimeOnly? plannedEnd = null,
        ZonedDeadline? deadline = null,
        string? location = null,
        string? description = null,
        string? materials = null,
        string? notes = null,
        Guid? seriesId = null,
        OccurrenceDate? occurrenceDate = null,
        bool isOccurrenceOverride = false,
        DateTimeOffset? completedAtUtc = null)
    {
        return new TaskItem(
            id,
            title,
            categoryId,
            priority,
            workflowStatus,
            plannedDate,
            plannedStart,
            plannedEnd,
            deadline,
            location,
            description,
            materials,
            notes,
            seriesId,
            occurrenceDate,
            isOccurrenceOverride,
            createdAtUtc,
            updatedAtUtc,
            completedAtUtc,
            version);
    }

    /// <summary>
    /// Gets the immutable task identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the normalized title.
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// Gets the referenced category identity. Existence is checked by Application.
    /// </summary>
    public Guid CategoryId { get; private set; }

    /// <summary>
    /// Gets the task priority.
    /// </summary>
    public TaskPriority Priority { get; private set; }

    /// <summary>
    /// Gets the persisted workflow state.
    /// </summary>
    public WorkflowStatus WorkflowStatus { get; private set; }

    /// <summary>
    /// Gets the optional local planned date.
    /// </summary>
    public DateOnly? PlannedDate { get; private set; }

    /// <summary>
    /// Gets the optional planned wall-clock range.
    /// </summary>
    public PlannedTimeRange PlannedTime { get; private set; }

    /// <summary>
    /// Gets the optional planned start wall-clock time.
    /// </summary>
    public TimeOnly? PlannedStart => PlannedTime.Start;

    /// <summary>
    /// Gets the optional planned end wall-clock time.
    /// </summary>
    public TimeOnly? PlannedEnd => PlannedTime.End;

    /// <summary>
    /// Gets the optional deadline with preserved local input and resolved UTC.
    /// </summary>
    public ZonedDeadline? Deadline { get; private set; }

    /// <summary>
    /// Gets the resolved UTC deadline, if any.
    /// </summary>
    public DateTimeOffset? DeadlineUtc => Deadline?.Utc;

    /// <summary>
    /// Gets the optional location.
    /// </summary>
    public string? Location { get; private set; }

    /// <summary>
    /// Gets the optional task description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the optional multi-line materials text.
    /// </summary>
    public string? Materials { get; private set; }

    /// <summary>
    /// Gets the optional multi-line notes text.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Gets the optional recurrence series identity.
    /// </summary>
    public Guid? SeriesId { get; }

    /// <summary>
    /// Gets the optional local occurrence date.
    /// </summary>
    public OccurrenceDate? OccurrenceDate { get; }

    /// <summary>
    /// Gets whether this recurrence instance has an instance-level override.
    /// </summary>
    public bool IsOccurrenceOverride { get; private set; }

    /// <summary>
    /// Gets the immutable creation timestamp normalized to UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// Gets the last caller-supplied update timestamp normalized to UTC.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the completion timestamp, present only for a completed task.
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>
    /// Gets the persistence version. DEV-010 does not advance it; the repository owns commit-time increments.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Updates editable task fields atomically after validating the complete candidate state.
    /// Workflow state, identity, recurrence identity, creation time and version are not changed here.
    /// </summary>
    public void UpdateDetails(
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
        string? notes,
        DateTimeOffset updatedAtUtc)
    {
        var values = ValidateEditableFields(
            title,
            categoryId,
            priority,
            plannedDate,
            plannedStart,
            plannedEnd,
            deadline,
            location,
            description,
            materials,
            notes);
        var normalizedUpdatedAt = ValidateUpdateTimestamp(updatedAtUtc);

        Title = values.Title;
        CategoryId = values.CategoryId;
        Priority = values.Priority;
        PlannedDate = values.PlannedDate;
        PlannedTime = values.PlannedTime;
        Deadline = values.Deadline;
        Location = values.Location;
        Description = values.Description;
        Materials = values.Materials;
        Notes = values.Notes;
        UpdatedAtUtc = normalizedUpdatedAt;
    }

    /// <summary>
    /// Starts processing a pending task. Repeating the operation while in progress is an idempotent no-op.
    /// A completed task must first be cancelled back to pending.
    /// </summary>
    public void StartProcessing(DateTimeOffset updatedAtUtc)
    {
        if (WorkflowStatus == WorkflowStatus.Completed)
        {
            throw new InvalidOperationException("A completed task must be cancelled before processing can restart.");
        }

        if (WorkflowStatus == WorkflowStatus.InProgress)
        {
            return;
        }

        var normalizedUpdatedAt = ValidateUpdateTimestamp(updatedAtUtc);
        WorkflowStatus = WorkflowStatus.InProgress;
        CompletedAtUtc = null;
        UpdatedAtUtc = normalizedUpdatedAt;
    }

    /// <summary>
    /// Completes a task using the same caller-supplied instant for completion and update metadata.
    /// Repeating completion is an idempotent no-op that preserves the first completion timestamp.
    /// </summary>
    public void Complete(DateTimeOffset completedAtUtc)
    {
        Complete(completedAtUtc, completedAtUtc);
    }

    /// <summary>
    /// Completes a task with explicit completion and update timestamps.
    /// Repeating completion preserves the first completion timestamp and all existing metadata.
    /// </summary>
    public void Complete(DateTimeOffset completedAtUtc, DateTimeOffset updatedAtUtc)
    {
        if (WorkflowStatus == WorkflowStatus.Completed)
        {
            return;
        }

        var normalizedCompletedAt = DomainValidation.NormalizeUtc(completedAtUtc, nameof(completedAtUtc));
        var normalizedUpdatedAt = ValidateUpdateTimestamp(updatedAtUtc);
        WorkflowStatus = WorkflowStatus.Completed;
        CompletedAtUtc = normalizedCompletedAt;
        UpdatedAtUtc = normalizedUpdatedAt;
    }

    /// <summary>
    /// Cancels completion and restores pending state. It deliberately does not restore a former in-progress state.
    /// Repeating cancellation for a non-completed task is an idempotent no-op.
    /// </summary>
    public void CancelCompletion(DateTimeOffset updatedAtUtc)
    {
        if (WorkflowStatus != WorkflowStatus.Completed)
        {
            return;
        }

        var normalizedUpdatedAt = ValidateUpdateTimestamp(updatedAtUtc);
        WorkflowStatus = WorkflowStatus.Pending;
        CompletedAtUtc = null;
        UpdatedAtUtc = normalizedUpdatedAt;
    }

    /// <summary>
    /// Marks a recurrence instance as overridden without changing its series or occurrence identity.
    /// </summary>
    public void MarkAsOccurrenceOverride(DateTimeOffset updatedAtUtc)
    {
        if (SeriesId is null || OccurrenceDate is null)
        {
            throw new InvalidOperationException("Only a recurrence instance can be marked as an occurrence override.");
        }

        if (IsOccurrenceOverride)
        {
            return;
        }

        var normalizedUpdatedAt = ValidateUpdateTimestamp(updatedAtUtc);
        IsOccurrenceOverride = true;
        UpdatedAtUtc = normalizedUpdatedAt;
    }

}
