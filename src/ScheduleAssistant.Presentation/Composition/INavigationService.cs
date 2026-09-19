using System.ComponentModel;
using ScheduleAssistant.Presentation.ViewModels;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>
/// Owns the single in-process navigation state for the main window.
/// </summary>
public interface INavigationService : INotifyPropertyChanged
{
    /// <summary>Gets all top-level destinations in display order.</summary>
    IReadOnlyList<NavigationItemViewModel> Items { get; }

    /// <summary>Gets the currently selected page.</summary>
    PageViewModelBase CurrentPage { get; }

    /// <summary>Gets the key of the currently selected page.</summary>
    NavigationPage CurrentPageKey { get; }

    /// <summary>Selects an existing page without creating another window.</summary>
    void Navigate(NavigationPage page);
}
