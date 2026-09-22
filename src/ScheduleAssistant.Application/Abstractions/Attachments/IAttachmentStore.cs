namespace ScheduleAssistant.Application.Abstractions.Attachments;

/// <summary>
/// Describes a file copied into the application-managed attachment directory.
/// </summary>
public sealed record StoredAttachment(
    Guid Id,
    Guid TaskId,
    string DisplayName,
    string ManagedRelativePath,
    string? Extension,
    string? MimeType,
    long SizeBytes,
    string Sha256);

/// <summary>
/// Provides the replaceable file-system boundary for managed attachments.
/// </summary>
public interface IAttachmentStore
{
    /// <summary>
    /// Copies a source file to a temporary file, calculates its metadata, and atomically commits it
    /// to the managed attachment directory.
    /// </summary>
    Task<StoredAttachment> ImportAsync(
        Guid taskId,
        Guid attachmentId,
        string sourcePath,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a managed attachment with the operating system default handler.</summary>
    Task OpenAsync(string managedRelativePath, CancellationToken cancellationToken = default);

    /// <summary>Reveals a managed attachment in the operating system file manager.</summary>
    Task RevealAsync(string managedRelativePath, CancellationToken cancellationToken = default);

    /// <summary>Deletes one managed file. Missing files are treated as already cleaned.</summary>
    Task DeleteAsync(string managedRelativePath, CancellationToken cancellationToken = default);

    /// <summary>Deletes the managed directory belonging to one deleted task.</summary>
    Task DeleteTaskDirectoryAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs one bounded recursive listing for startup orphan diagnosis. Returned paths are
    /// relative to the managed attachment root and never source-file paths.
    /// </summary>
    Task<IReadOnlyList<string>> ListManagedFilesAsync(CancellationToken cancellationToken = default);
}
