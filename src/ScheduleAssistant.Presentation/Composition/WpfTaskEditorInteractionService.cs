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
}
