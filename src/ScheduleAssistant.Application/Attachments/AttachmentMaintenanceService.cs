using System.IO;
using ScheduleAssistant.Application.Abstractions.Attachments;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Attachments;

/// <summary>
/// Handles post-delete attachment cleanup and one bounded startup maintenance pass.
/// </summary>
public sealed class AttachmentMaintenanceService
{
    private const int MaximumStartupQueueItems = 100;
    private const int MaximumAttemptsPerStartup = 3;

    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStore _attachmentStore;
    private readonly IAttachmentCleanupQueue _cleanupQueue;
    private readonly TimeProvider _timeProvider;
    private readonly Func<Guid> _newId;

    /// <summary>Initializes attachment lifecycle maintenance.</summary>
    public AttachmentMaintenanceService(
        IAttachmentRepository attachmentRepository,
        IAttachmentStore attachmentStore,
        IAttachmentCleanupQueue cleanupQueue,
        TimeProvider? timeProvider = null,
        Func<Guid>? idFactory = null)
    {
        _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
        _attachmentStore = attachmentStore ?? throw new ArgumentNullException(nameof(attachmentStore));
        _cleanupQueue = cleanupQueue ?? throw new ArgumentNullException(nameof(cleanupQueue));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _newId = idFactory ?? Guid.NewGuid;
    }

    /// <summary>
    /// Cleans a task directory after the task transaction has committed. A failure is recorded in
    /// cleanup_queue and never attempts to undo the task deletion.
    /// </summary>
    public async Task HandleTaskDeletedAsync(
        TaskDeleted applicationEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await _attachmentStore
                .DeleteTaskDirectoryAsync(applicationEvent.TaskId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await EnqueueTaskDirectoryCleanupAsync(
                applicationEvent.TaskId,
                FailureCode(exception),
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retries a bounded batch of queued cleanup items and performs one orphan diagnosis pass.
    /// This method does not schedule a timer or continue scanning after it returns.
    /// </summary>
    public async Task<AttachmentMaintenanceReport> RunStartupAsync(
        CancellationToken cancellationToken = default)
    {
        var pending = await _cleanupQueue
            .GetPendingAsync(MaximumStartupQueueItems, cancellationToken)
            .ConfigureAwait(false);
        var cleaned = 0;
        var stillPending = 0;

        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var succeeded = false;
            for (var attempt = 0; attempt < MaximumAttemptsPerStartup; attempt++)
            {
                try
                {
                    await DeleteQueuedItemAsync(item, cancellationToken).ConfigureAwait(false);
                    await _cleanupQueue.RemoveAsync(item.Id, cancellationToken).ConfigureAwait(false);
                    cleaned++;
                    succeeded = true;
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    await _cleanupQueue.RecordFailureAsync(
                        item.Id,
                        FailureCode(exception),
                        CancellationToken.None).ConfigureAwait(false);
                }
            }

            if (!succeeded)
            {
                stillPending++;
            }
        }

        var knownPaths = (await _attachmentRepository
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(attachment => attachment.ManagedRelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var managedFiles = await _attachmentStore
            .ListManagedFilesAsync(cancellationToken)
            .ConfigureAwait(false);
        var orphaned = managedFiles
            .Where(path => !knownPaths.Contains(NormalizeForComparison(path)))
            .Select(NormalizeForComparison)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AttachmentMaintenanceReport(
            pending.Count,
            cleaned,
            stillPending,
            orphaned.Length,
            orphaned);
    }

    private async Task DeleteQueuedItemAsync(
        AttachmentCleanupItem item,
        CancellationToken cancellationToken)
    {
        switch (item.ItemType)
        {
            case AttachmentCleanupItemTypes.ManagedFile:
                await _attachmentStore.DeleteAsync(item.ManagedRelativePath, cancellationToken).ConfigureAwait(false);
                return;
            case AttachmentCleanupItemTypes.TaskDirectory:
                await _attachmentStore.DeleteTaskDirectoryAsync(item.ItemId, cancellationToken).ConfigureAwait(false);
                return;
            default:
                throw new InvalidDataException("Unknown attachment cleanup item type.");
        }
    }

    private async Task EnqueueTaskDirectoryCleanupAsync(
        Guid taskId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        await _cleanupQueue.EnqueueAsync(
            new AttachmentCleanupItem(
                _newId(),
                AttachmentCleanupItemTypes.TaskDirectory,
                taskId,
                taskId.ToString("D"),
                _timeProvider.GetUtcNow().ToUniversalTime(),
                0,
                errorCode),
            cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeForComparison(string path)
    {
        return Attachment.NormalizeManagedRelativePath(path);
    }

    private static string FailureCode(Exception exception)
    {
        return exception switch
        {
            FileNotFoundException => "FileNotFound",
            DirectoryNotFoundException => "DirectoryNotFound",
            UnauthorizedAccessException => "AccessDenied",
            IOException => "IoFailure",
            InvalidDataException => "InvalidData",
            _ => "CleanupFailed"
        };
    }
}
