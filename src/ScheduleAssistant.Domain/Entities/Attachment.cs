namespace ScheduleAssistant.Domain;

/// <summary>
/// Metadata for a managed attachment. File content and all I/O remain outside the Domain layer.
/// </summary>
public sealed class Attachment
{
    private Attachment(
        Guid id,
        Guid taskId,
        string displayName,
        string managedRelativePath,
        string? extension,
        string? mimeType,
        long sizeBytes,
        string? sha256,
        DateTimeOffset importedAtUtc)
    {
        Id = DomainValidation.RequireNonEmpty(id, nameof(id));
        TaskId = DomainValidation.RequireNonEmpty(taskId, nameof(taskId));
        DisplayName = NormalizeDisplayName(displayName);
        ManagedRelativePath = NormalizeManagedRelativePath(managedRelativePath);
        Extension = DomainValidation.NormalizeExtension(extension, nameof(extension));
        MimeType = DomainValidation.NormalizeMetadata(mimeType, nameof(mimeType), 200);
        if (sizeBytes < 0)
        {
            throw new DomainValidationException("Attachment size cannot be negative.", nameof(sizeBytes));
        }

        SizeBytes = sizeBytes;
        Sha256 = DomainValidation.NormalizeHash(sha256, nameof(sha256));
        ImportedAtUtc = DomainValidation.NormalizeUtc(importedAtUtc, nameof(importedAtUtc));
    }

    /// <summary>
    /// Creates attachment metadata without opening or copying a file.
    /// </summary>
    public static Attachment Create(
        Guid id,
        Guid taskId,
        string displayName,
        string managedRelativePath,
        long sizeBytes,
        DateTimeOffset importedAtUtc,
        string? extension = null,
        string? mimeType = null,
        string? sha256 = null)
    {
        return new Attachment(
            id,
            taskId,
            displayName,
            managedRelativePath,
            extension,
            mimeType,
            sizeBytes,
            sha256,
            importedAtUtc);
    }

    /// <summary>
    /// Rebuilds attachment metadata from persistence without accessing the file system.
    /// </summary>
    public static Attachment Rehydrate(
        Guid id,
        Guid taskId,
        string displayName,
        string managedRelativePath,
        long sizeBytes,
        string? extension,
        string? mimeType,
        string? sha256,
        DateTimeOffset importedAtUtc)
    {
        return new Attachment(
            id,
            taskId,
            displayName,
            managedRelativePath,
            extension,
            mimeType,
            sizeBytes,
            sha256,
            importedAtUtc);
    }

    /// <summary>
    /// Gets the immutable attachment identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the immutable owning task identity.
    /// </summary>
    public Guid TaskId { get; }

    /// <summary>
    /// Gets the original display name, including supported spaces and non-ASCII characters.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the normalized forward-slash relative path under the managed attachment root.
    /// </summary>
    public string ManagedRelativePath { get; }

    /// <summary>
    /// Gets the optional lowercase extension, stored with a leading period.
    /// </summary>
    public string? Extension { get; }

    /// <summary>
    /// Gets the optional MIME metadata.
    /// </summary>
    public string? MimeType { get; }

    /// <summary>
    /// Gets the non-negative size in bytes.
    /// </summary>
    public long SizeBytes { get; }

    /// <summary>
    /// Gets the optional lowercase SHA-256 hash.
    /// </summary>
    public string? Sha256 { get; }

    /// <summary>
    /// Gets the import timestamp normalized to UTC.
    /// </summary>
    public DateTimeOffset ImportedAtUtc { get; }

    /// <summary>
    /// Validates and normalizes a managed attachment path without checking the machine file system.
    /// </summary>
    public static string NormalizeManagedRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new DomainValidationException("Managed attachment path must not be blank.", nameof(path));
        }

        if (path.Contains('\0'))
        {
            throw new DomainValidationException("Managed attachment path contains a NUL character.", nameof(path));
        }

        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/')
            || normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':')
        {
            throw new DomainValidationException("Managed attachment path must be relative.", nameof(path));
        }

        var segments = normalized.Split('/', StringSplitOptions.None);
        var safeSegments = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == ".." || segment.Contains(':'))
            {
                throw new DomainValidationException("Managed attachment path contains a traversal or drive segment.", nameof(path));
            }

            safeSegments.Add(segment);
        }

        if (safeSegments.Count == 0)
        {
            throw new DomainValidationException("Managed attachment path must contain a file segment.", nameof(path));
        }

        return string.Join('/', safeSegments);
    }

    private static string NormalizeDisplayName(string? value)
    {
        var displayName = DomainValidation.RequireNonBlank(value, nameof(value));
        DomainValidation.EnsureNoControlCharacters(displayName, nameof(value));
        if (displayName.Contains('/') || displayName.Contains('\\'))
        {
            throw new DomainValidationException("Attachment display name must not contain path separators.", nameof(value));
        }

        return displayName;
    }
}
