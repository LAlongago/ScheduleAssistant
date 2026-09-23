namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Performs the small amount of WPF window lifecycle work owned by the composition root.</summary>
public interface IWindowService
{
    /// <summary>Shows the already-constructed main window.</summary>
    void ShowMainWindow(MainWindow mainWindow);

    /// <summary>Restores and activates the main window if it is currently hidden or minimized.</summary>
    void ActivateMainWindow();
}
