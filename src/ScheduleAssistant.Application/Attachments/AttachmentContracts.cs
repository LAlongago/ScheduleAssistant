using ScheduleAssistant.Application.Common;

namespace ScheduleAssistant.Application.Attachments;

/// <summary>Attachment metadata exposed to Presentation without a Domain dependency.</summary>
public sealed record AttachmentDto(
    Guid Id,
    Guid TaskId,
    string DisplayName,
    string ManagedRelativePath,
    string? Extension,
    string? MimeType,
    long SizeBytes,
    string? Sha256,
    DateTimeOffset ImportedAtUtc);

/// <summary>Queries attachments belonging to one task.</summary>
public sealed record GetAttachmentsByTaskQuery(Guid TaskId);

/// <summary>Imports one source file into the managed attachment store.</summary>
public sealed record ImportAttachmentCommand(Guid TaskId, string SourcePath);

/// <summary>Opens one attachment belonging to a task.</summary>
public sealed record OpenAttachmentCommand(Guid TaskId, Guid AttachmentId);

/// <summary>Reveals one attachment belonging to a task.</summary>
public sealed record RevealAttachmentCommand(Guid TaskId, Guid AttachmentId);

/// <summary>Changes only the display name of an attachment.</summary>
public sealed record RenameAttachmentCommand(
    Guid TaskId,
    Guid AttachmentId,
    string DisplayName);

/// <summary>Removes one attachment after Presentation has obtained confirmation.</summary>
public sealed record RemoveAttachmentCommand(Guid TaskId, Guid AttachmentId);

/// <summary>Describes the result of removing attachment metadata and its managed file.</summary>
public sealed record AttachmentRemovalResult(Guid AttachmentId, bool CleanupQueued);

/// <summary>One startup maintenance report; no source paths are included.</summary>
public sealed record AttachmentMaintenanceReport(
    int QueueItemsInspected,
    int QueueItemsCleaned,
    int QueueItemsStillPending,
    int OrphanFileCount,
    IReadOnlyList<string> OrphanedManagedRelativePaths);
