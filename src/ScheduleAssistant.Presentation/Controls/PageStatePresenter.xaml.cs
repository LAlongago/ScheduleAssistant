using System.Windows;
using System.Windows.Controls;

namespace ScheduleAssistant.Presentation.Controls;

/// <summary>
/// Reusable loading, empty, error, and normal-content presenter.
/// </summary>
public partial class PageStatePresenter : UserControl
{
    /// <summary>Identifies the state dependency property.</summary>
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(PageContentState),
        typeof(PageStatePresenter),
        new PropertyMetadata(PageContentState.Ready));

    /// <summary>Identifies the status-title dependency property.</summary>
    public static readonly DependencyProperty StatusTitleProperty = DependencyProperty.Register(
        nameof(StatusTitle),
        typeof(string),
        typeof(PageStatePresenter),
        new PropertyMetadata(string.Empty));

    /// <summary>Identifies the status-message dependency property.</summary>
    public static readonly DependencyProperty StatusMessageProperty = DependencyProperty.Register(
        nameof(StatusMessage),
        typeof(string),
        typeof(PageStatePresenter),
        new PropertyMetadata(string.Empty));

    /// <summary>Identifies the normal-content dependency property.</summary>
    public static readonly DependencyProperty NormalContentProperty = DependencyProperty.Register(
        nameof(NormalContent),
        typeof(object),
        typeof(PageStatePresenter),
        new PropertyMetadata(null));

    /// <summary>Initializes the presenter.</summary>
    public PageStatePresenter()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the current page content state.</summary>
    public PageContentState State
    {
        get => (PageContentState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Gets or sets the status heading.</summary>
    public string StatusTitle
    {
        get => (string)GetValue(StatusTitleProperty);
        set => SetValue(StatusTitleProperty, value);
    }

    /// <summary>Gets or sets the status explanation.</summary>
    public string StatusMessage
    {
        get => (string)GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    /// <summary>Gets or sets the content shown when <see cref="State"/> is Ready.</summary>
    public object? NormalContent
    {
        get => GetValue(NormalContentProperty);
        set => SetValue(NormalContentProperty, value);
    }
}
