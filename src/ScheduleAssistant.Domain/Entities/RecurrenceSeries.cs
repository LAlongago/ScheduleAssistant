namespace ScheduleAssistant.Domain;

/// <summary>
/// A recurrence template and validated snapshot of fields shared by its materialized instances.
/// The <see cref="RecurrenceRule"/> is the sole authority for effective dates and time-zone information.
/// </summary>
public sealed class RecurrenceSeries
{
    private RecurrenceSeries(
        Guid id,
        string title,
        Guid categoryId,
        TaskPriority priority,
        RecurrenceRule rule,
        TimeOnly? plannedStart,
        TimeOnly? plannedEnd,
        string? location,
        string? description,
        string? materials,
        string? notes,
        bool isEnabled,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        long version)
    {
        Id = DomainValidation.RequireNonEmpty(id, nameof(id));
        var values = ValidateEditableFields(
            title,
            categoryId,
            priority,
            rule,
            plannedStart,
            plannedEnd,
            location,
            description,
            materials,
            notes);

        Title = values.Title;
        CategoryId = values.CategoryId;
        Priority = values.Priority;
        Rule = values.Rule;
        PlannedTime = values.PlannedTime;
        Location = values.Location;
        Description = values.Description;
        Materials = values.Materials;
        Notes = values.Notes;
        IsEnabled = isEnabled;
        CreatedAtUtc = DomainValidation.NormalizeUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = DomainValidation.NormalizeUtc(updatedAtUtc, nameof(updatedAtUtc));
        DomainValidation.EnsureAtOrAfter(UpdatedAtUtc, CreatedAtUtc, nameof(updatedAtUtc));
        Version = ValidateVersion(version);
    }

    /// <summary>
    /// Creates an enabled recurrence series with version one.
    /// Deadline templates are intentionally not inferred from the occurrence date in DEV-010.
    /// </summary>
    public static RecurrenceSeries Create(
        Guid id,
        string title,
        Guid categoryId,
        TaskPriority priority,
        RecurrenceRule rule,
        DateTimeOffset createdAtUtc,
        TimeOnly? plannedStart = null,
        TimeOnly? plannedEnd = null,
        string? location = null,
        string? description = null,
        string? materials = null,
        string? notes = null,
        bool isEnabled = true)
    {
        return new RecurrenceSeries(
            id,
            title,
            categoryId,
            priority,
            rule,
            plannedStart,
            plannedEnd,
            location,
            description,
            materials,
            notes,
            isEnabled,
            createdAtUtc,
            createdAtUtc,
            1);
    }

    /// <summary>
    /// Rebuilds a recurrence series from persistence without replaying creation behavior.
    /// </summary>
    public static RecurrenceSeries Rehydrate(
        Guid id,
        string title,
        Guid categoryId,
        TaskPriority priority,
        RecurrenceRule rule,
        TimeOnly? plannedStart,
        TimeOnly? plannedEnd,
        string? location,
        string? description,
        string? materials,
        string? notes,
        bool isEnabled,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        long version)
    {
        return new RecurrenceSeries(
            id,
            title,
            categoryId,
            priority,
            rule,
            plannedStart,
            plannedEnd,
            location,
            description,
            materials,
            notes,
            isEnabled,
            createdAtUtc,
            updatedAtUtc,
            version);
    }

    /// <summary>
    /// Gets the immutable series identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the normalized shared title.
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// Gets the referenced category identity. Existence is checked by Application.
    /// </summary>
    public Guid CategoryId { get; private set; }

    /// <summary>
    /// Gets the shared task priority.
    /// </summary>
    public TaskPriority Priority { get; private set; }

    /// <summary>
    /// Gets the authoritative recurrence rule.
    /// </summary>
    public RecurrenceRule Rule { get; private set; }

    /// <summary>
    /// Gets the optional shared planned wall-clock range.
    /// </summary>
    public PlannedTimeRange PlannedTime { get; private set; }

    /// <summary>
    /// Gets the optional shared planned start time.
    /// </summary>
    public TimeOnly? PlannedStart => PlannedTime.Start;

    /// <summary>
    /// Gets the optional shared planned end time.
    /// </summary>
    public TimeOnly? PlannedEnd => PlannedTime.End;

    /// <summary>
    /// Gets the optional shared location.
    /// </summary>
    public string? Location { get; private set; }

    /// <summary>
    /// Gets the optional shared description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the optional shared materials text.
    /// </summary>
    public string? Materials { get; private set; }

    /// <summary>
    /// Gets the optional shared notes text.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Gets whether future materialization is enabled.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Gets the rule's effective date without creating a second source of truth.
    /// </summary>
    public DateOnly EffectiveDate => Rule.EffectiveDate;

    /// <summary>
    /// Gets the rule's time-zone ID without storing a duplicate.
    /// </summary>
    public string TimeZoneId => Rule.TimeZoneId;

    /// <summary>
    /// Gets the immutable creation timestamp in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// Gets the last caller-supplied update timestamp in UTC.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the persistence version. Commit-time increments belong to the repository.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Updates shared series fields atomically after validating the complete candidate state.
    /// </summary>
    public void UpdateDetails(
        string title,
        Guid categoryId,
        TaskPriority priority,
        RecurrenceRule rule,
        TimeOnly? plannedStart,
        TimeOnly? plannedEnd,
        string? location,
        string? description,
        string? materials,
        string? notes,
        bool isEnabled,
        DateTimeOffset updatedAtUtc)
    {
        var values = ValidateEditableFields(
            title,
            categoryId,
            priority,
            rule,
            plannedStart,
            plannedEnd,
            location,
            description,
            materials,
            notes);
        var normalizedUpdatedAt = NormalizeUpdateTimestamp(updatedAtUtc);

        Title = values.Title;
        CategoryId = values.CategoryId;
        Priority = values.Priority;
        Rule = values.Rule;
        PlannedTime = values.PlannedTime;
        Location = values.Location;
        Description = values.Description;
        Materials = values.Materials;
        Notes = values.Notes;
        IsEnabled = isEnabled;
        UpdatedAtUtc = normalizedUpdatedAt;
    }

    private static EditableValues ValidateEditableFields(
        string title,
        Guid categoryId,
        TaskPriority priority,
        RecurrenceRule rule,
        TimeOnly? plannedStart,
        TimeOnly? plannedEnd,
        string? location,
        string? description,
        string? materials,
        string? notes)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return new EditableValues(
            DomainValidation.NormalizeTitle(title, nameof(title), TaskItem.MaximumTitleLength),
            DomainValidation.RequireNonEmpty(categoryId, nameof(categoryId)),
            DomainValidation.RequireDefinedEnum(priority, nameof(priority)),
            rule,
            new PlannedTimeRange(plannedStart, plannedEnd),
            DomainValidation.NormalizeOptionalText(location, nameof(location), TaskItem.MaximumLocationLength),
            DomainValidation.NormalizeOptionalText(description, nameof(description), TaskItem.MaximumLongTextLength),
            DomainValidation.NormalizeOptionalText(materials, nameof(materials), TaskItem.MaximumLongTextLength),
            DomainValidation.NormalizeOptionalText(notes, nameof(notes), TaskItem.MaximumLongTextLength));
    }

    private DateTimeOffset NormalizeUpdateTimestamp(DateTimeOffset value)
    {
        var normalized = DomainValidation.NormalizeUtc(value, nameof(value));
        DomainValidation.EnsureAtOrAfter(normalized, CreatedAtUtc, nameof(value));
        DomainValidation.EnsureAtOrAfter(normalized, UpdatedAtUtc, nameof(value));
        return normalized;
    }

    private static long ValidateVersion(long version)
    {
        if (version < 1)
        {
            throw new DomainValidationException("Version must be at least one.", nameof(version));
        }

        return version;
    }

    private sealed class EditableValues
    {
        public EditableValues(
            string title,
            Guid categoryId,
            TaskPriority priority,
            RecurrenceRule rule,
            PlannedTimeRange plannedTime,
            string? location,
            string? description,
            string? materials,
            string? notes)
        {
            Title = title;
            CategoryId = categoryId;
            Priority = priority;
            Rule = rule;
            PlannedTime = plannedTime;
            Location = location;
            Description = description;
            Materials = materials;
            Notes = notes;
        }

        public string Title { get; }

        public Guid CategoryId { get; }

        public TaskPriority Priority { get; }

        public RecurrenceRule Rule { get; }

        public PlannedTimeRange PlannedTime { get; }

        public string? Location { get; }

        public string? Description { get; }

        public string? Materials { get; }

        public string? Notes { get; }
    }
}
