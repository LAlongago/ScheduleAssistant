using CommunityToolkit.Mvvm.ComponentModel;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>
/// Describes one keyboard-accessible item in the shell navigation.
/// </summary>
public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isSelected;

    /// <summary>
    /// Initializes a navigation item.
    /// </summary>
    public NavigationItemViewModel(
        NavigationPage page,
        string label,
        string iconGlyph,
        string tooltip)
    {
        Page = page;
        Label = label;
        IconGlyph = iconGlyph;
        Tooltip = tooltip;
    }

    /// <summary>Gets the destination represented by this item.</summary>
    public NavigationPage Page { get; }

    /// <summary>Gets the visible destination label.</summary>
    public string Label { get; }

    /// <summary>Gets the text glyph used as a compact icon.</summary>
    public string IconGlyph { get; }

    /// <summary>Gets the tooltip and accessible description.</summary>
    public string Tooltip { get; }

    /// <summary>Gets the name exposed to UI Automation.</summary>
    public string AccessibleName => $"{Label}页面";

    /// <summary>Gets or sets whether this item is the current destination.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
