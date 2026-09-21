using System.Diagnostics.CodeAnalysis;

namespace ScheduleAssistant.Application.Abstractions.Attachments;

/// <summary>Stable item-type values stored in the existing cleanup_queue table.</summary>
public static class AttachmentCleanupItemTypes
{
    /// <summary>Identifies one managed attachment file.</summary>
    public const string ManagedFile = "attachment-file";

    /// <summary>Identifies the directory belonging to a deleted task.</summary>
    public const string TaskDirectory = "task-directory";
}

/// <summary>One bounded cleanup operation persisted for a later retry.</summary>
public sealed record AttachmentCleanupItem(
    Guid Id,
    string ItemType,
    Guid ItemId,
    string ManagedRelativePath,
    DateTimeOffset QueuedAtUtc,
    int AttemptCount,
    string? LastErrorCode);

/// <summary>Persistence port for the existing cleanup_queue table.</summary>
[SuppressMessage(
    "Naming",
    "CA1711",
    Justification = "The port deliberately names the durable cleanup queue it abstracts.")]
public interface IAttachmentCleanupQueue
{
    /// <summary>Adds a cleanup item without touching the file system.</summary>
    Task EnqueueAsync(
        AttachmentCleanupItem item,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a bounded batch of pending cleanup items.</summary>
    Task<IReadOnlyList<AttachmentCleanupItem>> GetPendingAsync(
        int maximumItems,
        CancellationToken cancellationToken = default);

    /// <summary>Removes an item after its managed path has been cleaned.</summary>
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Records one failed cleanup attempt using a non-sensitive error code.</summary>
    Task RecordFailureAsync(
        Guid id,
        string errorCode,
        CancellationToken cancellationToken = default);
}
