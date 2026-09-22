namespace ScheduleAssistant.Presentation.Composition;

/// <summary>One source selected through the native file picker.</summary>
public sealed record AttachmentFileSelection(string SourcePath, string DisplayName);

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

    /// <summary>Opens a multi-select file picker without reading the selected files.</summary>
    IReadOnlyList<AttachmentFileSelection> SelectAttachmentFiles();

    /// <summary>Asks for a new user-visible attachment display name.</summary>
    string? PromptAttachmentDisplayName(string currentDisplayName);

    /// <summary>Confirms removal of one attachment.</summary>
    bool ConfirmRemoveAttachment(string displayName);
}
