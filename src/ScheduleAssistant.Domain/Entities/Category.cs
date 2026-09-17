namespace ScheduleAssistant.Domain;

/// <summary>
/// A user-visible task category and its non-UI color metadata.
/// </summary>
public sealed class Category
{
    /// <summary>
    /// Maximum category name length in Unicode scalar values.
    /// </summary>
    public const int MaximumNameLength = 100;

    private Category(
        Guid id,
        string name,
        string colorHex,
        int sortOrder,
        bool isBuiltIn,
        bool isArchived,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        long version)
    {
        Id = DomainValidation.RequireNonEmpty(id, nameof(id));
        Name = DomainValidation.NormalizeName(name, nameof(name), MaximumNameLength);
        ColorHex = DomainValidation.NormalizeColorHex(colorHex, nameof(colorHex));
        IsBuiltIn = isBuiltIn;
        IsArchived = isArchived;
        SortOrder = sortOrder;
        CreatedAtUtc = DomainValidation.NormalizeUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = DomainValidation.NormalizeUtc(updatedAtUtc, nameof(updatedAtUtc));
        DomainValidation.EnsureAtOrAfter(UpdatedAtUtc, CreatedAtUtc, nameof(updatedAtUtc));
        Version = ValidateVersion(version);
    }

    /// <summary>
    /// Creates a new, unarchived category with version one.
    /// Colors use the canonical uppercase <c>#RRGGBB</c> storage format.
    /// </summary>
    public static Category Create(
        Guid id,
        string name,
        string colorHex,
        int sortOrder,
        DateTimeOffset createdAtUtc,
        bool isBuiltIn = false)
    {
        return new Category(id, name, colorHex, sortOrder, isBuiltIn, false, createdAtUtc, createdAtUtc, 1);
    }

    /// <summary>
    /// Rebuilds a category from persistence without applying creation defaults.
    /// </summary>
    public static Category Rehydrate(
        Guid id,
        string name,
        string colorHex,
        int sortOrder,
        bool isBuiltIn,
        bool isArchived,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        long version)
    {
        return new Category(id, name, colorHex, sortOrder, isBuiltIn, isArchived, createdAtUtc, updatedAtUtc, version);
    }

    /// <summary>
    /// Gets the immutable category identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the normalized category name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the canonical uppercase <c>#RRGGBB</c> color string.
    /// </summary>
    public string ColorHex { get; private set; }

    /// <summary>
    /// Gets the ordering value used by Application/Presentation.
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Gets whether this category is a built-in category.
    /// </summary>
    public bool IsBuiltIn { get; }

    /// <summary>
    /// Gets whether this category is archived.
    /// </summary>
    public bool IsArchived { get; private set; }

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
    /// Updates category metadata atomically.
    /// </summary>
    public void UpdateDetails(
        string name,
        string colorHex,
        int sortOrder,
        bool isArchived,
        DateTimeOffset updatedAtUtc)
    {
        var normalizedName = DomainValidation.NormalizeName(name, nameof(name), MaximumNameLength);
        var normalizedColor = DomainValidation.NormalizeColorHex(colorHex, nameof(colorHex));
        var normalizedUpdatedAt = NormalizeUpdateTimestamp(updatedAtUtc);

        Name = normalizedName;
        ColorHex = normalizedColor;
        SortOrder = sortOrder;
        IsArchived = isArchived;
        UpdatedAtUtc = normalizedUpdatedAt;
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
}
