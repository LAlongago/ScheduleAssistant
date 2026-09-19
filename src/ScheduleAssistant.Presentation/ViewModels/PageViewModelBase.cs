using CommunityToolkit.Mvvm.ComponentModel;
using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>
/// Common presentation state for a top-level page. It contains no business query or persistence logic.
/// </summary>
public abstract class PageViewModelBase : ObservableObject
{
    private PageContentState _contentState;
    private string _statusTitle;
    private string _statusMessage;

    /// <summary>
    /// Initializes a page with its shell title and state text.
    /// </summary>
    protected PageViewModelBase(
        string title,
        string description,
        PageContentState contentState,
        string statusTitle,
        string statusMessage)
    {
        Title = title;
        Description = description;
        _contentState = contentState;
        _statusTitle = statusTitle;
        _statusMessage = statusMessage;
    }

    /// <summary>Gets the page title shown in the shell header.</summary>
    public string Title { get; }

    /// <summary>Gets the short page description shown below the title.</summary>
    public string Description { get; }

    /// <summary>Gets the current content state.</summary>
    public PageContentState ContentState
    {
        get => _contentState;
        private set => SetProperty(ref _contentState, value);
    }

    /// <summary>Gets the status heading used for loading, empty, and error states.</summary>
    public string StatusTitle
    {
        get => _statusTitle;
        private set => SetProperty(ref _statusTitle, value);
    }

    /// <summary>Gets the status explanation shown below the status heading.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Changes only presentation state. Real query results and error mapping belong to later tasks.
    /// </summary>
    public void SetContentState(PageContentState state, string statusTitle, string statusMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statusTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(statusMessage);

        ContentState = state;
        StatusTitle = statusTitle;
        StatusMessage = statusMessage;
    }
}
