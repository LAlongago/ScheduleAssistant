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
        var result = MessageBox.Show(
            "当前编辑器有未保存修改。确定放弃这些修改吗？",
            "放弃修改",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }
}
