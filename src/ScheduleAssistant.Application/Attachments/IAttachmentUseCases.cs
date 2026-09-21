using ScheduleAssistant.Application.Common;

namespace ScheduleAssistant.Application.Attachments;

/// <summary>
/// Independent attachment use cases. Task use cases intentionally do not contain attachment methods.
/// </summary>
public interface IAttachmentUseCases
{
    /// <summary>Lists persisted attachment metadata for a task.</summary>
    Task<ApplicationResult<IReadOnlyList<AttachmentDto>>> GetByTaskIdAsync(
        GetAttachmentsByTaskQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Imports one source file and persists its metadata.</summary>
    Task<ApplicationResult<AttachmentDto>> ImportAsync(
        ImportAttachmentCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Opens one managed attachment.</summary>
    Task<ApplicationResult<AttachmentDto>> OpenAsync(
        OpenAttachmentCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Reveals one managed attachment in the file manager.</summary>
    Task<ApplicationResult<AttachmentDto>> RevealAsync(
        RevealAttachmentCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Renames only the user-visible display name.</summary>
    Task<ApplicationResult<AttachmentDto>> RenameAsync(
        RenameAttachmentCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Removes metadata and then cleans or queues the managed file.</summary>
    Task<ApplicationResult<AttachmentRemovalResult>> RemoveAsync(
        RemoveAttachmentCommand command,
        CancellationToken cancellationToken = default);
}
