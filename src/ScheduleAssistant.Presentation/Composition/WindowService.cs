namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Default one-window lifecycle adapter for the WPF composition root.</summary>
public sealed class WindowService : IWindowService
{
    private MainWindow? _mainWindow;

    /// <inheritdoc />
    public void ShowMainWindow(MainWindow mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        _mainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <inheritdoc />
    public void ActivateMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
        {
            _mainWindow.WindowState = System.Windows.WindowState.Normal;
        }

        _mainWindow.Activate();
    }
}
