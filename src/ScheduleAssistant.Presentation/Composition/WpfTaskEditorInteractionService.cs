using System.IO;
using Microsoft.Win32;
using System.Windows;
using ScheduleAssistant.Presentation.Views;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>WPF dialog implementation of the task-editor interaction port.</summary>
public sealed class WpfTaskEditorInteractionService : ITaskEditorInteractionService
{
    /// <inheritdoc />
    public bool ConfirmDeadlineBeforePlannedDate(DateOnly plannedDate, DateOnly deadlineDate)
    {
        var result = MessageBox.Show(
            $"Deadline ({deadlineDate:yyyy-MM-dd}) 早于计划日期 ({plannedDate:yyyy-MM-dd})。仍要保存吗？",
            "确认 Deadline",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }

    /// <inheritdoc />
    public DateTimeOffset? ChooseAmbiguousDeadline(
        DateOnly localDate,
        TimeOnly localTime,
        string timeZoneId,
        IReadOnlyList<DateTimeOffset> candidates)
    {
        var window = new AmbiguousDeadlineChoiceWindow(localDate, localTime, timeZoneId, candidates);
        return window.ShowDialog() == true ? window.SelectedUtc : null;
    }

    /// <inheritdoc />
    public bool ConfirmDiscardChanges()
    {
        var owner = System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current?.MainWindow;
        var dialog = new ConfirmDiscardChangesWindow();
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }

    /// <inheritdoc />
    public IReadOnlyList<AttachmentFileSelection> SelectAttachmentFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            CheckPathExists = true,
            Filter = "常见文件|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.png;*.jpg;*.jpeg;*.gif;*.zip;*.rar|所有文件|*.*",
            FilterIndex = 1,
            Title = "选择附件"
        };
        if (dialog.ShowDialog() != true)
        {
            return Array.Empty<AttachmentFileSelection>();
        }

        return dialog.FileNames
            .Select(path => new AttachmentFileSelection(path, Path.GetFileName(path)))
            .Where(selection => !string.IsNullOrWhiteSpace(selection.DisplayName))
            .ToArray();
    }

    /// <inheritdoc />
    public string? PromptAttachmentDisplayName(string currentDisplayName)
    {
        var owner = System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current?.MainWindow;
        var dialog = new AttachmentDisplayNameWindow(currentDisplayName);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true ? dialog.SelectedDisplayName : null;
    }

    /// <inheritdoc />
    public bool ConfirmRemoveAttachment(string displayName)
    {
        var result = MessageBox.Show(
            $"确认移除附件“{displayName}”？这只会删除应用管理副本，不会删除最初选择的源文件。",
            "确认移除附件",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }

    /// <inheritdoc />
    public bool ConfirmRecurrenceOperation(string title, string message, string confirmLabel)
    {
        var owner = System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current?.MainWindow;
        var dialog = new ConfirmRecurrenceActionWindow(title, message, confirmLabel);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }
}
