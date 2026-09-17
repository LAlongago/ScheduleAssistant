using System.ComponentModel;
using System.Text;
using System.Windows;

namespace ScheduleAssistant.Spike001.Notifications;

public partial class MainWindow : Window
{
    private readonly Action _sendNotification;
    private readonly Action _refreshCapability;
    private readonly Action _hideWindow;
    private readonly Action _exitApplication;
    private bool _allowClose;
    private readonly StringBuilder _diagnostics = new();

    public MainWindow(
        Action sendNotification,
        Action refreshCapability,
        Action hideWindow,
        Action exitApplication)
    {
        _sendNotification = sendNotification;
        _refreshCapability = refreshCapability;
        _hideWindow = hideWindow;
        _exitApplication = exitApplication;
        InitializeComponent();
    }

    public void AppendDiagnostic(string message)
    {
        _diagnostics.AppendLine(message);
        DiagnosticsTextBox.Text = _diagnostics.ToString();
        DiagnosticsTextBox.ScrollToEnd();
    }

    public void SetCapabilityStatus(string text)
    {
        CapabilityText.Text = text;
    }

    public void SetRuntimeDiagnostics(string text)
    {
        RuntimeTextBox.Text = text;
    }

    public void SetActivationResult(string taskId, string source, int activationCount, int processId)
    {
        ActivationText.Text =
            $"识别到模拟任务 ID：{taskId}\n" +
            $"来源：{source}\n" +
            $"业务实例数：1（当前进程 PID={processId}）\n" +
            $"本进程累计激活次数：{activationCount}";
    }

    public void RequestClose()
    {
        _allowClose = true;
        Close();
    }

    private void SendNotificationClick(object sender, RoutedEventArgs e)
    {
        _sendNotification();
    }

    private void RefreshCapabilityClick(object sender, RoutedEventArgs e)
    {
        _refreshCapability();
    }

    private void HideWindowClick(object sender, RoutedEventArgs e)
    {
        _hideWindow();
    }

    private void ExitClick(object sender, RoutedEventArgs e)
    {
        _exitApplication();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        _hideWindow();
    }
}
