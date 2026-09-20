namespace ScheduleAssistant.Presentation.Composition;

/// <summary>
/// UI-only interactions required by the task editor. ViewModels use this port instead of WPF dialogs.
/// </summary>
public interface ITaskEditorInteractionService
{
    /// <summary>Asks whether a deadline before the planned date should be saved.</summary>
    bool ConfirmDeadlineBeforePlannedDate(DateOnly plannedDate, DateOnly deadlineDate);

    /// <summary>Lets the user choose one of the two valid UTC instants for an ambiguous local time.</summary>
    DateTimeOffset? ChooseAmbiguousDeadline(
        DateOnly localDate,
        TimeOnly localTime,
        string timeZoneId,
        IReadOnlyList<DateTimeOffset> candidates);

    /// <summary>Asks whether unsaved editor changes should be discarded.</summary>
    bool ConfirmDiscardChanges();
}
