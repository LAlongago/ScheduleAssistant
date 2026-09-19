namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Default one-window lifecycle adapter for the WPF composition root.</summary>
public sealed class WindowService : IWindowService
{
    /// <inheritdoc />
    public void ShowMainWindow(MainWindow mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        mainWindow.Show();
    }
}
