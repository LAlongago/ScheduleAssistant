using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ScheduleAssistant.Spike002;

/// <summary>
/// Holds only simulated dashboard data and display diagnostics; it has no Win32 dependency.
/// </summary>
public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly CultureInfo _displayCulture = CultureInfo.CurrentCulture;
    private DesktopHostResult _hostResult = new(
        DesktopHostMode.WidgetFallback,
        "启动为普通无边框 Widget；不会固定在桌面底层。",
        "none",
        0);

    /// <summary>
    /// Initializes the simulated dashboard.
    /// </summary>
    public DashboardViewModel()
    {
        var today = DateTime.Today;
        Tasks =
        [
            new SimulatedTask("整理技术验证结论", today.AddHours(18).AddMinutes(30)),
            new SimulatedTask("准备明日会议材料", today.AddDays(1).AddHours(10)),
            new SimulatedTask("检查桌面交互路径", today.AddDays(1).AddHours(16)),
            new SimulatedTask("记录 Explorer 恢复结果", today.AddDays(2).AddHours(20)),
        ];

        foreach (var task in Tasks)
        {
            task.CompletionChanged += OnTaskCompletionChanged;
        }
    }

    /// <summary>
    /// Raised when a simulated checkbox changes.
    /// </summary>
    public event EventHandler? TaskCompletionChanged;

    /// <summary>
    /// Raised when a display property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the four simulated tasks.
    /// </summary>
    public ObservableCollection<SimulatedTask> Tasks { get; }

    /// <summary>
    /// Gets the current local date and weekday.
    /// </summary>
    public string CurrentDateText => DateTime.Now.ToString("yyyy年M月d日 dddd", _displayCulture);

    /// <summary>
    /// Gets the host mode for display.
    /// </summary>
    public string HostModeText => _hostResult.ModeLabel;

    /// <summary>
    /// Gets the current host diagnostic reason.
    /// </summary>
    public string HostDiagnosticText => _hostResult.Reason;

    /// <summary>
    /// Gets the selected parent kind and bounded attempt count.
    /// </summary>
    public string HostDetailsText =>
        $"Parent: {_hostResult.ParentKind}; 尝试次数: {_hostResult.Attempts}";

    /// <summary>
    /// Gets a summary of simulated completion state.
    /// </summary>
    public string CompletionSummary =>
        $"已完成 {Tasks.Count(task => task.IsCompleted)}/{Tasks.Count}";

    /// <summary>
    /// Applies an observable host result without exposing native handles to the view model.
    /// </summary>
    public void ApplyHostResult(DesktopHostResult result)
    {
        _hostResult = result;
        OnPropertyChanged(nameof(HostModeText));
        OnPropertyChanged(nameof(HostDiagnosticText));
        OnPropertyChanged(nameof(HostDetailsText));
    }

    private void OnTaskCompletionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CompletionSummary));
        TaskCompletionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
