using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScheduleAssistant.Spike002;

/// <summary>
/// Minimal dashboard shell for the isolated desktop-host experiment.
/// </summary>
public partial class MainWindow : Window, IDisposable
{
    private readonly DashboardViewModel _viewModel = new();
    private readonly DesktopHostService _desktopHost = new();
    private bool _loaded;
    private bool _closing;

    /// <summary>
    /// Initializes the prototype window.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
        _viewModel.TaskCompletionChanged += OnTaskCompletionChanged;
        _desktopHost.StatusChanged += OnHostStatusChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await RunHostActionAsync(
            () => _desktopHost.PrepareWidgetAsync(this, CancellationToken.None));
    }

    private void OnHostStatusChanged(object? sender, DesktopHostStatusChangedEventArgs e)
    {
        _viewModel.ApplyHostResult(e.Result);
    }

    private async void OnAttachClick(object sender, RoutedEventArgs e)
    {
        await RunHostActionAsync(
            () => _desktopHost.AttachAsync(this, CancellationToken.None));
    }

    private async void OnDetachClick(object sender, RoutedEventArgs e)
    {
        await RunHostActionAsync(
            () => _desktopHost.DetachAsync(this, CancellationToken.None));
    }

    private async void OnWidgetClick(object sender, RoutedEventArgs e)
    {
        await RunHostActionAsync(
            () => _desktopHost.ShowWidgetAsync(this, CancellationToken.None));
    }

    private async void OnRestorePositionClick(object sender, RoutedEventArgs e)
    {
        await RunHostActionAsync(
            () => _desktopHost.PrepareWidgetAsync(this, CancellationToken.None));
    }

    private void OnTaskCompletionChanged(object? sender, EventArgs e)
    {
        if (_desktopHost.IsEmbeddedPendingInteraction)
        {
            _viewModel.ApplyHostResult(_desktopHost.MarkInteractionVerified());
        }
    }

    private void OnDragSurfaceMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _desktopHost.IsEmbedded)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can race a native shell transition; the widget remains usable.
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_closing)
        {
            return;
        }

        e.Cancel = true;
        _closing = true;
        try
        {
            await _desktopHost.DetachAsync(this, CancellationToken.None);
        }
        catch
        {
            // Detach is best effort during close; the host also restores in Dispose.
        }
        finally
        {
            _desktopHost.Dispose();
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Close));
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.TaskCompletionChanged -= OnTaskCompletionChanged;
        _desktopHost.StatusChanged -= OnHostStatusChanged;
        Dispose();
    }

    /// <summary>
    /// Releases the isolated host resources.
    /// </summary>
    public void Dispose()
    {
        _desktopHost.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task RunHostActionAsync(Func<Task<DesktopHostResult>> action)
    {
        try
        {
            _viewModel.ApplyHostResult(await action());
        }
        catch (OperationCanceledException)
        {
            _viewModel.ApplyHostResult(
                new DesktopHostResult(
                    DesktopHostMode.WidgetFallback,
                    "操作被取消；保持普通 Widget。",
                    "none",
                    0));
        }
        catch (Exception exception)
        {
            _viewModel.ApplyHostResult(
                new DesktopHostResult(
                    DesktopHostMode.Unavailable,
                    $"宿主操作失败：{exception.Message}",
                    "unknown",
                    0));
        }
    }
}
