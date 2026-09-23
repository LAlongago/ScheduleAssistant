using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScheduleAssistant.Application.Attachments;
using ScheduleAssistant.Application.Common;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>One attachment row shown by the task editor.</summary>
public sealed partial class AttachmentEditorItemViewModel : ObservableObject
{
    private string _displayName;
    private string _sizeText;
    private string _statusText;
    private bool _isPending;

    internal AttachmentEditorItemViewModel(
        string displayName,
        long sizeBytes,
        bool isPending,
        string? sourcePath = null,
        Guid? id = null)
    {
        Id = id ?? Guid.Empty;
        SourcePath = sourcePath;
        _displayName = displayName;
        _sizeText = FormatSize(sizeBytes);
        _isPending = isPending;
        _statusText = isPending ? "待任务保存后导入" : string.Empty;
    }

    /// <summary>Gets the persisted attachment identity, or empty for a staged item.</summary>
    public Guid Id { get; }

    /// <summary>Gets or sets the display name shown to the user.</summary>
    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    /// <summary>Gets a compact localized size representation.</summary>
    public string SizeText
    {
        get => _sizeText;
        private set => SetProperty(ref _sizeText, value);
    }

    /// <summary>Gets the current import or cleanup status.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Gets whether this row still refers to a source file awaiting import.</summary>
    public bool IsPending
    {
        get => _isPending;
        private set
        {
            if (!SetProperty(ref _isPending, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanOpen));
            OnPropertyChanged(nameof(CanReveal));
            OnPropertyChanged(nameof(CanRename));
        }
    }

    /// <summary>Gets whether the managed copy can be opened.</summary>
    public bool CanOpen => !IsPending;

    /// <summary>Gets whether the managed copy can be revealed.</summary>
    public bool CanReveal => !IsPending;

    /// <summary>Gets whether the persisted display name can be changed.</summary>
    public bool CanRename => !IsPending;

    internal string? SourcePath { get; private set; }

    internal void ApplyImported(AttachmentDto attachment)
    {
        DisplayName = attachment.DisplayName;
        SizeText = FormatSize(attachment.SizeBytes);
        SourcePath = null;
        StatusText = string.Empty;
        IsPending = false;
    }

    internal void ApplyRenamed(string displayName)
    {
        DisplayName = displayName;
        StatusText = string.Empty;
    }

    internal void SetFailure(string message)
    {
        StatusText = message;
    }

    private static string FormatSize(long sizeBytes)
    {
        if (sizeBytes < 1_024)
        {
            return $"{sizeBytes:N0} B";
        }

        if (sizeBytes < 1_048_576)
        {
            return $"{sizeBytes / 1_024d:N1} KB";
        }

        if (sizeBytes < 1_073_741_824)
        {
            return $"{sizeBytes / 1_048_576d:N1} MB";
        }

        return $"{sizeBytes / 1_073_741_824d:N1} GB";
    }
}

public sealed partial class TaskEditorViewModel
{
    private readonly ObservableCollection<AttachmentEditorItemViewModel> _attachmentItems = [];
    private string? _attachmentErrorMessage;

    private void InitializeAttachmentState()
    {
        AttachmentItems = new ReadOnlyObservableCollection<AttachmentEditorItemViewModel>(_attachmentItems);
        AddAttachmentCommand = new AsyncRelayCommand(AddAttachmentsAsync, CanAddAttachment);
        OpenAttachmentCommand = new AsyncRelayCommand<AttachmentEditorItemViewModel>(
            OpenAttachmentAsync,
            CanOpenAttachment);
        RevealAttachmentCommand = new AsyncRelayCommand<AttachmentEditorItemViewModel>(
            RevealAttachmentAsync,
            CanOpenAttachment);
        RenameAttachmentCommand = new AsyncRelayCommand<AttachmentEditorItemViewModel>(
            RenameAttachmentAsync,
            CanRenameAttachment);
        RemoveAttachmentCommand = new AsyncRelayCommand<AttachmentEditorItemViewModel>(
            RemoveAttachmentAsync,
            CanRemoveAttachment);
    }

    /// <summary>Gets attachments currently displayed by the editor.</summary>
    public ReadOnlyObservableCollection<AttachmentEditorItemViewModel> AttachmentItems { get; private set; } = null!;

    /// <summary>Gets whether at least one attachment row is displayed.</summary>
    public bool HasAttachments => _attachmentItems.Count != 0;

    /// <summary>Gets whether the attachment list has no rows.</summary>
    public bool IsAttachmentListEmpty => !HasAttachments;

    /// <summary>Gets a safe attachment-specific error or warning.</summary>
    public string? AttachmentErrorMessage
    {
        get => _attachmentErrorMessage;
        private set
        {
            if (!SetProperty(ref _attachmentErrorMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasAttachmentError));
        }
    }

    /// <summary>Gets whether an attachment error or partial-import warning is visible.</summary>
    public bool HasAttachmentError => !string.IsNullOrWhiteSpace(AttachmentErrorMessage);

    /// <summary>Gets the command that stages files selected from the native picker.</summary>
    public IAsyncRelayCommand AddAttachmentCommand { get; private set; } = null!;

    /// <summary>Gets the command that opens a managed attachment.</summary>
    public IAsyncRelayCommand<AttachmentEditorItemViewModel> OpenAttachmentCommand { get; private set; } = null!;

    /// <summary>Gets the command that reveals a managed attachment.</summary>
    public IAsyncRelayCommand<AttachmentEditorItemViewModel> RevealAttachmentCommand { get; private set; } = null!;

    /// <summary>Gets the command that changes a persisted display name.</summary>
    public IAsyncRelayCommand<AttachmentEditorItemViewModel> RenameAttachmentCommand { get; private set; } = null!;

    /// <summary>Gets the command that confirms and removes an attachment.</summary>
    public IAsyncRelayCommand<AttachmentEditorItemViewModel> RemoveAttachmentCommand { get; private set; } = null!;

    private async Task LoadAttachmentsAsync(CancellationToken cancellationToken)
    {
        ClearAttachmentRows();
        AttachmentErrorMessage = null;
        if (_attachmentUseCases is null || !IsEditMode || !_persistedTaskId.HasValue)
        {
            return;
        }

        var result = await _attachmentUseCases.GetByTaskIdAsync(
            new GetAttachmentsByTaskQuery(_persistedTaskId.Value),
            cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            SetAttachmentError(result.Error?.Message ?? "无法加载附件列表，请重试。");
            return;
        }

        foreach (var attachment in result.Value)
        {
            _attachmentItems.Add(CreatePersistedRow(attachment));
        }

        NotifyAttachmentCollectionChanged();
    }

    private async Task AddAttachmentsAsync()
    {
        if (!CanAddAttachment())
        {
            return;
        }

        var selected = _interactionService.SelectAttachmentFiles();
        foreach (var selection in selected)
        {
            if (string.IsNullOrWhiteSpace(selection.SourcePath)
                || string.IsNullOrWhiteSpace(selection.DisplayName))
            {
                continue;
            }

            _attachmentItems.Add(
                new AttachmentEditorItemViewModel(
                    selection.DisplayName,
                    0,
                    isPending: true,
                    sourcePath: selection.SourcePath));
        }

        if (selected.Count != 0)
        {
            AttachmentErrorMessage = null;
            MarkChanged(nameof(AttachmentItems));
            NotifyAttachmentCollectionChanged();
        }

        await Task.CompletedTask;
    }

    private async Task OpenAttachmentAsync(AttachmentEditorItemViewModel? item)
    {
        if (item is null || !CanOpenAttachment(item) || !_persistedTaskId.HasValue)
        {
            return;
        }

        var result = await _attachmentUseCases!.OpenAsync(
            new OpenAttachmentCommand(_persistedTaskId.Value, item.Id));
        HandleAttachmentOperationResult(result, item, "打开附件失败，请重试。");
    }

    private async Task RevealAttachmentAsync(AttachmentEditorItemViewModel? item)
    {
        if (item is null || !CanOpenAttachment(item) || !_persistedTaskId.HasValue)
        {
            return;
        }

        var result = await _attachmentUseCases!.RevealAsync(
            new RevealAttachmentCommand(_persistedTaskId.Value, item.Id));
        HandleAttachmentOperationResult(result, item, "定位附件失败，请重试。");
    }

    private async Task RenameAttachmentAsync(AttachmentEditorItemViewModel? item)
    {
        if (item is null || !CanRenameAttachment(item) || !_persistedTaskId.HasValue)
        {
            return;
        }

        var displayName = _interactionService.PromptAttachmentDisplayName(item.DisplayName);
        if (string.IsNullOrWhiteSpace(displayName)
            || string.Equals(displayName, item.DisplayName, StringComparison.Ordinal))
        {
            return;
        }

        var result = await _attachmentUseCases!.RenameAsync(
            new RenameAttachmentCommand(_persistedTaskId.Value, item.Id, displayName));
        if (!result.IsSuccess || result.Value is null)
        {
            SetAttachmentError(result.Error?.Message ?? "重命名附件失败，请重试。");
            item.SetFailure("重命名失败");
            return;
        }

        item.ApplyRenamed(result.Value.DisplayName);
        AttachmentErrorMessage = null;
    }

    private async Task RemoveAttachmentAsync(AttachmentEditorItemViewModel? item)
    {
        if (item is null
            || !CanRemoveAttachment(item)
            || !_interactionService.ConfirmRemoveAttachment(item.DisplayName))
        {
            return;
        }

        if (item.IsPending)
        {
            _attachmentItems.Remove(item);
            MarkChanged(nameof(AttachmentItems));
            NotifyAttachmentCollectionChanged();
            await Task.CompletedTask;
            return;
        }

        if (!_persistedTaskId.HasValue)
        {
            return;
        }

        var result = await _attachmentUseCases!.RemoveAsync(
            new RemoveAttachmentCommand(_persistedTaskId.Value, item.Id));
        if (!result.IsSuccess)
        {
            SetAttachmentError(result.Error?.Message ?? "移除附件失败，请重试。");
            item.SetFailure("移除失败");
            return;
        }

        _attachmentItems.Remove(item);
        MarkChanged(nameof(AttachmentItems));
        if (result.Value?.CleanupQueued == true)
        {
            SetAttachmentError("附件记录已移除，但管理文件将在后台重试清理。");
        }

        NotifyAttachmentCollectionChanged();
    }

    private async Task<bool> ImportPendingAttachmentsAsync(Guid taskId)
    {
        if (_attachmentUseCases is null)
        {
            return true;
        }

        var succeeded = true;
        var pending = _attachmentItems.Where(item => item.IsPending).ToArray();
        foreach (var item in pending)
        {
            if (string.IsNullOrWhiteSpace(item.SourcePath))
            {
                item.SetFailure("导入失败：未找到源文件。");
                succeeded = false;
                continue;
            }

            ApplicationResult<AttachmentDto> result;
            try
            {
                result = await _attachmentUseCases.ImportAsync(
                    new ImportAttachmentCommand(taskId, item.SourcePath));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                item.SetFailure("导入失败：附件存储不可用。");
                succeeded = false;
                continue;
            }

            if (!result.IsSuccess || result.Value is null)
            {
                item.SetFailure($"导入失败：{result.Error?.Message ?? "请重试"}");
                succeeded = false;
                continue;
            }

            item.ApplyImported(result.Value);
        }

        if (!succeeded)
        {
            SetAttachmentError("部分附件导入失败，请检查每行状态后重试。");
            _isDirty = true;
            OnPropertyChanged(nameof(IsDirty));
            NotifyAttachmentCollectionChanged();
        }
        else if (pending.Length != 0)
        {
            AttachmentErrorMessage = null;
        }

        return succeeded;
    }

    private bool CanAddAttachment()
    {
        return CanManageAttachments && IsInitialized && !IsBusy;
    }

    private bool CanOpenAttachment(AttachmentEditorItemViewModel? item)
    {
        return CanManageAttachments && IsInitialized && !IsBusy
            && item is not null && item.CanOpen && item.Id != Guid.Empty;
    }

    private bool CanRenameAttachment(AttachmentEditorItemViewModel? item)
    {
        return CanManageAttachments && IsInitialized && !IsBusy
            && item is not null && item.CanRename && item.Id != Guid.Empty;
    }

    private bool CanRemoveAttachment(AttachmentEditorItemViewModel? item)
    {
        return CanManageAttachments && IsInitialized && !IsBusy && item is not null;
    }

    private void HandleAttachmentOperationResult<T>(
        ApplicationResult<T> result,
        AttachmentEditorItemViewModel item,
        string fallback)
    {
        if (result.IsSuccess)
        {
            AttachmentErrorMessage = null;
            item.SetFailure(string.Empty);
            return;
        }

        SetAttachmentError(result.Error?.Message ?? fallback);
        item.SetFailure("操作失败");
    }

    private void SetAttachmentError(string message)
    {
        AttachmentErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "附件操作失败，请重试。"
            : message;
    }

    private static AttachmentEditorItemViewModel CreatePersistedRow(AttachmentDto attachment)
    {
        return new AttachmentEditorItemViewModel(
            attachment.DisplayName,
            attachment.SizeBytes,
            isPending: false,
            id: attachment.Id);
    }

    private void ClearAttachmentRows()
    {
        _attachmentItems.Clear();
        NotifyAttachmentCollectionChanged();
    }

    private void NotifyAttachmentCollectionChanged()
    {
        OnPropertyChanged(nameof(AttachmentItems));
        OnPropertyChanged(nameof(HasAttachments));
        OnPropertyChanged(nameof(IsAttachmentListEmpty));
        NotifyAttachmentCommandState();
    }

    private void NotifyAttachmentCommandState()
    {
        AddAttachmentCommand.NotifyCanExecuteChanged();
        OpenAttachmentCommand.NotifyCanExecuteChanged();
        RevealAttachmentCommand.NotifyCanExecuteChanged();
        RenameAttachmentCommand.NotifyCanExecuteChanged();
        RemoveAttachmentCommand.NotifyCanExecuteChanged();
    }
}
