using System.Windows;

namespace ScheduleAssistant.Spike002;

/// <summary>
/// Composition root for the isolated SPIKE-002 WPF prototype.
/// </summary>
public partial class App : System.Windows.Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
