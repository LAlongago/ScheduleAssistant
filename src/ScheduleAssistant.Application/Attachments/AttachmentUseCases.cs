using System.IO;
using ScheduleAssistant.Application.Abstractions.Attachments;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Application.Attachments;

/// <summary>
/// Orchestrates attachment metadata, managed-file operations, and compensation boundaries.
/// </summary>
public sealed class AttachmentUseCases : IAttachmentUseCases
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IPersistenceTransactionFactory _transactionFactory;
    private readonly IAttachmentStore _attachmentStore;
    private readonly IAttachmentCleanupQueue _cleanupQueue;
    private readonly TimeProvider _timeProvider;
    private readonly Func<Guid> _newId;

    /// <summary>Initializes independent attachment use cases.</summary>
    public AttachmentUseCases(
        IAttachmentRepository attachmentRepository,
        ITaskRepository taskRepository,
        IPersistenceTransactionFactory transactionFactory,
        IAttachmentStore attachmentStore,
        IAttachmentCleanupQueue cleanupQueue,
        TimeProvider? timeProvider = null,
        Func<Guid>? idFactory = null)
    {
        _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
        _attachmentStore = attachmentStore ?? throw new ArgumentNullException(nameof(attachmentStore));
        _cleanupQueue = cleanupQueue ?? throw new ArgumentNullException(nameof(cleanupQueue));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _newId = idFactory ?? Guid.NewGuid;
    }

    /// <inheritdoc />
    public Task<ApplicationResult<IReadOnlyList<AttachmentDto>>> GetByTaskIdAsync(
        GetAttachmentsByTaskQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("List", async () =>
        {
            ArgumentNullException.ThrowIfNull(query);
            EnsureIdentity(query.TaskId, nameof(query.TaskId));
            var attachments = await _attachmentRepository
                .GetByTaskIdAsync(query.TaskId, cancellationToken)
                .ConfigureAwait(false);
            return ApplicationResult<IReadOnlyList<AttachmentDto>>.Success(
                attachments.Select(ToDto).ToArray());
        });
    }

    /// <inheritdoc />
    public Task<ApplicationResult<AttachmentDto>> ImportAsync(
        ImportAttachmentCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Import", () => ImportCoreAsync(command, cancellationToken));
    }

    /// <inheritdoc />
    public Task<ApplicationResult<AttachmentDto>> OpenAsync(
        OpenAttachmentCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Open", async () =>
        {
            var attachment = await GetOwnedAsync(command, cancellationToken).ConfigureAwait(false);
            if (attachment is null)
            {
                return ApplicationResult<AttachmentDto>.Failure(NotFound());
            }

            await _attachmentStore
                .OpenAsync(attachment.ManagedRelativePath, cancellationToken)
                .ConfigureAwait(false);
            return ApplicationResult<AttachmentDto>.Success(ToDto(attachment));
        });
    }

    /// <inheritdoc />
    public Task<ApplicationResult<AttachmentDto>> RevealAsync(
        RevealAttachmentCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Reveal", async () =>
        {
            var attachment = await GetOwnedAsync(command, cancellationToken).ConfigureAwait(false);
            if (attachment is null)
            {
                return ApplicationResult<AttachmentDto>.Failure(NotFound());
            }

            await _attachmentStore
                .RevealAsync(attachment.ManagedRelativePath, cancellationToken)
                .ConfigureAwait(false);
            return ApplicationResult<AttachmentDto>.Success(ToDto(attachment));
        });
    }

    /// <inheritdoc />
    public Task<ApplicationResult<AttachmentDto>> RenameAsync(
        RenameAttachmentCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Rename", async () =>
        {
            ArgumentNullException.ThrowIfNull(command);
            var attachment = await GetOwnedAsync(command, cancellationToken).ConfigureAwait(false);
            if (attachment is null)
            {
                return ApplicationResult<AttachmentDto>.Failure(NotFound());
            }

            var renamed = Attachment.Rehydrate(
                attachment.Id,
                attachment.TaskId,
                command.DisplayName,
                attachment.ManagedRelativePath,
                attachment.SizeBytes,
                attachment.Extension,
                attachment.MimeType,
                attachment.Sha256,
                attachment.ImportedAtUtc);
            await _attachmentRepository
                .UpdateDisplayNameAsync(renamed, cancellationToken)
                .ConfigureAwait(false);
            return ApplicationResult<AttachmentDto>.Success(ToDto(renamed));
        });
    }

    /// <inheritdoc />
    public Task<ApplicationResult<AttachmentRemovalResult>> RemoveAsync(
        RemoveAttachmentCommand command,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("Remove", () => RemoveCoreAsync(command, cancellationToken));
    }

    private async Task<ApplicationResult<AttachmentDto>> ImportCoreAsync(
        ImportAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureIdentity(command.TaskId, nameof(command.TaskId));
        if (string.IsNullOrWhiteSpace(command.SourcePath))
        {
            return ApplicationResult<AttachmentDto>.Failure(
                ApplicationErrorMapper.Validation(
                    "Attachment.SourceRequired",
                    "请选择一个仍然存在的源文件。"));
        }

        var task = await _taskRepository
            .GetByIdAsync(command.TaskId, cancellationToken)
            .ConfigureAwait(false);
        if (task is null)
        {
            return ApplicationResult<AttachmentDto>.Failure(
                new ApplicationError(
                    ApplicationErrorKind.NotFound,
                    "Task.NotFound",
                    "任务不存在，无法导入附件。"));
        }

        var attachmentId = _newId();
        StoredAttachment? stored = null;
        try
        {
            stored = await _attachmentStore
                .ImportAsync(command.TaskId, attachmentId, command.SourcePath, cancellationToken)
                .ConfigureAwait(false);
            var metadata = Attachment.Create(
                stored.Id,
                stored.TaskId,
                stored.DisplayName,
                stored.ManagedRelativePath,
                stored.SizeBytes,
                _timeProvider.GetUtcNow().ToUniversalTime(),
                stored.Extension,
                stored.MimeType,
                stored.Sha256);

            await using var transaction = await _transactionFactory
                .BeginAsync(cancellationToken)
                .ConfigureAwait(false);
            await _attachmentRepository
                .AddAsync(metadata, transaction, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ApplicationResult<AttachmentDto>.Success(ToDto(metadata));
        }
        catch
        {
            if (stored is not null)
            {
                await CompensateFileAsync(stored, CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }

    private async Task<ApplicationResult<AttachmentRemovalResult>> RemoveCoreAsync(
        RemoveAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var attachment = await GetOwnedAsync(command, cancellationToken).ConfigureAwait(false);
        if (attachment is null)
        {
            return ApplicationResult<AttachmentRemovalResult>.Failure(NotFound());
        }

        await using (var transaction = await _transactionFactory
                         .BeginAsync(cancellationToken)
                         .ConfigureAwait(false))
        {
            await _attachmentRepository
                .DeleteAsync(attachment.Id, transaction, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var cleanupQueued = false;
        try
        {
            await _attachmentStore
                .DeleteAsync(attachment.ManagedRelativePath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            cleanupQueued = await EnqueueFileCleanupAsync(
                attachment,
                FailureCode(exception),
                CancellationToken.None).ConfigureAwait(false);
        }

        var warnings = cleanupQueued
            ? new[]
            {
                new ApplicationWarning(
                    "Attachment.CleanupQueued",
                    "附件记录已移除，但管理副本将在下次启动继续清理。")
            }
            : null;
        return ApplicationResult<AttachmentRemovalResult>.Success(
            new AttachmentRemovalResult(attachment.Id, cleanupQueued),
            warnings);
    }

    private async Task<Attachment?> GetOwnedAsync(
        OpenAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureIdentity(command.TaskId, nameof(command.TaskId));
        EnsureIdentity(command.AttachmentId, nameof(command.AttachmentId));
        return await GetOwnedAsync(command.TaskId, command.AttachmentId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Attachment?> GetOwnedAsync(
        RevealAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureIdentity(command.TaskId, nameof(command.TaskId));
        EnsureIdentity(command.AttachmentId, nameof(command.AttachmentId));
        return await GetOwnedAsync(command.TaskId, command.AttachmentId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Attachment?> GetOwnedAsync(
        RenameAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureIdentity(command.TaskId, nameof(command.TaskId));
        EnsureIdentity(command.AttachmentId, nameof(command.AttachmentId));
        return await GetOwnedAsync(command.TaskId, command.AttachmentId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Attachment?> GetOwnedAsync(
        RemoveAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureIdentity(command.TaskId, nameof(command.TaskId));
        EnsureIdentity(command.AttachmentId, nameof(command.AttachmentId));
        return await GetOwnedAsync(command.TaskId, command.AttachmentId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Attachment?> GetOwnedAsync(
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository
            .GetByIdAsync(attachmentId, cancellationToken)
            .ConfigureAwait(false);
        return attachment?.TaskId == taskId ? attachment : null;
    }

    private async Task CompensateFileAsync(
        StoredAttachment stored,
        CancellationToken cancellationToken)
    {
        try
        {
            await _attachmentStore
                .DeleteAsync(stored.ManagedRelativePath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await TryEnqueueAsync(
                new AttachmentCleanupItem(
                    _newId(),
                    AttachmentCleanupItemTypes.ManagedFile,
                    stored.Id,
                    stored.ManagedRelativePath,
                    _timeProvider.GetUtcNow().ToUniversalTime(),
                    0,
                    FailureCode(exception)),
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<bool> EnqueueFileCleanupAsync(
        Attachment attachment,
        string errorCode,
        CancellationToken cancellationToken)
    {
        return await TryEnqueueAsync(
                new AttachmentCleanupItem(
                    _newId(),
                    AttachmentCleanupItemTypes.ManagedFile,
                    attachment.Id,
                    attachment.ManagedRelativePath,
                    _timeProvider.GetUtcNow().ToUniversalTime(),
                    0,
                    errorCode),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> TryEnqueueAsync(
        AttachmentCleanupItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            await _cleanupQueue.EnqueueAsync(item, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static AttachmentDto ToDto(Attachment attachment)
    {
        return new AttachmentDto(
            attachment.Id,
            attachment.TaskId,
            attachment.DisplayName,
            attachment.ManagedRelativePath,
            attachment.Extension,
            attachment.MimeType,
            attachment.SizeBytes,
            attachment.Sha256,
            attachment.ImportedAtUtc);
    }

    private static ApplicationError NotFound()
    {
        return new ApplicationError(
            ApplicationErrorKind.NotFound,
            "Attachment.NotFound",
            "附件不存在或不属于当前任务。");
    }

    private static void EnsureIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A non-empty identity is required.", parameterName);
        }
    }

    private static string FailureCode(Exception exception)
    {
        return exception switch
        {
            FileNotFoundException => "FileNotFound",
            DirectoryNotFoundException => "DirectoryNotFound",
            UnauthorizedAccessException => "AccessDenied",
            IOException => "IoFailure",
            _ => "StorageFailure"
        };
    }

    private static async Task<ApplicationResult<T>> ExecuteAsync<T>(
        string operation,
        Func<Task<ApplicationResult<T>>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ApplicationResult<T>.Failure(MapException(operation, exception));
        }
    }

    private static ApplicationError MapException(string operation, Exception exception)
    {
        if (operation == "Import" && exception is FileNotFoundException)
        {
            return new ApplicationError(
                ApplicationErrorKind.StorageUnavailable,
                "Attachment.SourceUnavailable",
                "无法读取所选源文件，请确认文件仍然存在。");
        }

        if (exception is FileNotFoundException)
        {
            return new ApplicationError(
                ApplicationErrorKind.NotFound,
                "Attachment.ManagedFileNotFound",
                "附件管理副本不存在，请运行一次清理诊断或重新导入。");
        }

        if (exception is IOException or UnauthorizedAccessException or TimeoutException)
        {
            return new ApplicationError(
                ApplicationErrorKind.StorageUnavailable,
                "Attachment.StorageUnavailable",
                "附件存储暂时不可用，请检查磁盘和目录权限。");
        }

        return ApplicationErrorMapper.Map(exception);
    }
}
