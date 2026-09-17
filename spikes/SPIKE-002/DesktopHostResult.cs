namespace ScheduleAssistant.Spike002;

/// <summary>
/// The observable result of the experimental desktop host.
/// </summary>
public enum DesktopHostMode
{
    /// <summary>
    /// The native parent was changed, but a real checkbox interaction is still required.
    /// </summary>
    EmbeddedPendingInteraction,

    /// <summary>
    /// The native parent was changed and a checkbox interaction was observed.
    /// </summary>
    Embedded,

    /// <summary>
    /// The prototype is a normal, non-topmost, interactive widget.
    /// </summary>
    WidgetFallback,

    /// <summary>
    /// No usable window state could be established.
    /// </summary>
    Unavailable,
}

/// <summary>
/// Diagnostic state returned by <see cref="DesktopHostService"/>.
/// </summary>
public sealed record DesktopHostResult(
    DesktopHostMode Mode,
    string Reason,
    string ParentKind,
    int Attempts)
{
    /// <summary>
    /// Gets a human-readable mode label for the prototype UI.
    /// </summary>
    public string ModeLabel => Mode switch
    {
        DesktopHostMode.EmbeddedPendingInteraction => "Embedded · 待交互验证",
        DesktopHostMode.Embedded => "Embedded",
        DesktopHostMode.WidgetFallback => "WidgetFallback",
        DesktopHostMode.Unavailable => "Unavailable",
        _ => Mode.ToString(),
    };
}

/// <summary>
/// Event arguments for a host status change caused by a shell or display event.
/// </summary>
public sealed class DesktopHostStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes event arguments.
    /// </summary>
    public DesktopHostStatusChangedEventArgs(DesktopHostResult result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets the new host result.
    /// </summary>
    public DesktopHostResult Result { get; }
}
