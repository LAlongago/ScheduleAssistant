using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ScheduleAssistant.Spike002;

/// <summary>
/// Isolates all WorkerW/Progman discovery, Win32 calls, parent switching and restoration.
/// </summary>
public sealed partial class DesktopHostService : IDisposable
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int GwlHwndParent = -8;
    private const int GwOwner = 4;
    private const uint ProgmanWorkerMessage = 0x052C;
    private const uint SmtoNormal = 0x0000;
    private const int WmDisplayChange = 0x007E;
    private const int WmDpiChanged = 0x02E0;
    private const int WmSettingChange = 0x001A;
    private const uint WsChild = 0x40000000;
    private const uint WsPopup = 0x80000000;
    private const uint WsCaption = 0x00C00000;
    private const uint WsThickFrame = 0x00040000;
    private const uint WsMinimizeBox = 0x00020000;
    private const uint WsMaximizeBox = 0x00010000;
    private const uint WsSysMenu = 0x00080000;
    private const uint WsVisible = 0x10000000;
    private const uint WsExAppWindow = 0x00040000;
    private const uint WsExWindowEdge = 0x00000100;
    private const uint SwShownoactivate = 4;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const uint MonitorDefaultToNearest = 2;

    private static readonly IntPtr HwndBottom = new(1);
    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private static readonly TimeSpan[] AttachBackoff =
    [
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(750),
    ];

    private Window? _window;
    private HwndSource? _source;
    private HwndSourceHook? _hook;
    private IntPtr _windowHandle;
    private IntPtr _originalParent;
    private IntPtr _originalOwner;
    private IntPtr _embeddedParent;
    private long _originalStyle;
    private long _originalExStyle;
    private bool _hasSnapshot;
    private bool _interactionVerified;
    private bool _recoveryPending;
    private uint _taskbarCreatedMessage;
    private Rect? _widgetBounds;
    private string _parentKind = "none";
    private DesktopHostResult _lastResult = new(
        DesktopHostMode.WidgetFallback,
        "尚未请求桌面嵌入；当前为普通 Widget。",
        "none",
        0);

    /// <summary>
    /// Raised when a shell/display message causes the host to recover or fall back.
    /// </summary>
    public event EventHandler<DesktopHostStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Gets the latest result known by the host.
    /// </summary>
    public DesktopHostResult CurrentResult => _lastResult;

    /// <summary>
    /// Gets a value indicating whether a checkbox click is still needed to validate embedding.
    /// </summary>
    public bool IsEmbeddedPendingInteraction =>
        _embeddedParent != IntPtr.Zero && !_interactionVerified;

    /// <summary>
    /// Gets a value indicating whether the window is currently a child of a desktop host.
    /// </summary>
    public bool IsEmbedded => _embeddedParent != IntPtr.Zero;

    /// <summary>
    /// Prepares the window as a visible, ordinary widget.
    /// </summary>
    public Task<DesktopHostResult> PrepareWidgetAsync(Window window, CancellationToken cancellationToken)
    {
        VerifyUiThread(window);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSnapshot(window);
        StartMonitoring(window);
        return Task.FromResult(EnsureWidgetVisible(window, "普通 Widget 已准备；不使用 Topmost。"));
    }

    /// <summary>
    /// Attempts a bounded WorkerW/Progman attach, then falls back to the visible widget.
    /// </summary>
    public async Task<DesktopHostResult> AttachAsync(Window window, CancellationToken cancellationToken)
    {
        VerifyUiThread(window);
        EnsureSnapshot(window);
        StartMonitoring(window);
        _interactionVerified = false;

        var lastFailure = "未发现可用的 WorkerW/Progman 父窗口。";
        for (var index = 0; index < AttachBackoff.Length; index++)
        {
            if (index > 0)
            {
                await Task.Delay(AttachBackoff[index], cancellationToken);
            }

            var attempt = index + 1;
            var parent = DiscoverDesktopParent();
            if (parent.Handle == IntPtr.Zero)
            {
                lastFailure = parent.Reason;
                continue;
            }

            try
            {
                CaptureWidgetBounds(window);
                ApplyEmbeddedWindow(window, parent);
                _lastResult = new DesktopHostResult(
                    DesktopHostMode.EmbeddedPendingInteraction,
                    $"已附着到 {parent.Kind}；请现在勾选任一模拟任务。只有复选框状态真实变化后才判定 Embedded。",
                    parent.Kind,
                    attempt);
                return _lastResult;
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
            {
                lastFailure = $"{parent.Kind} 附着失败：{exception.Message}";
                TryRestoreWidget(window);
            }
        }

        var fallback = EnsureWidgetVisible(
            window,
            $"WorkerW/Progman 未能稳定附着（有限重试 {AttachBackoff.Length} 次）：{lastFailure} 已回退为普通 Widget。{Environment.NewLine}普通 Widget 不承诺固定在桌面底层。",
            AttachBackoff.Length);
        return fallback;
    }

    /// <summary>
    /// Restores the normal top-level window without making it topmost.
    /// </summary>
    public Task<DesktopHostResult> DetachAsync(Window window, CancellationToken cancellationToken)
    {
        VerifyUiThread(window);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSnapshot(window);
        var result = EnsureWidgetVisible(window, "已退出桌面嵌入；恢复为普通 Widget。", 0);
        _interactionVerified = false;
        return Task.FromResult(result);
    }

    /// <summary>
    /// Explicitly selects the ordinary widget fallback and reports that choice.
    /// </summary>
    public Task<DesktopHostResult> ShowWidgetAsync(Window window, CancellationToken cancellationToken)
    {
        VerifyUiThread(window);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSnapshot(window);
        var result = EnsureWidgetVisible(window, "已切换为普通 Widget；不承诺固定在桌面底层。", 0);
        _interactionVerified = false;
        return Task.FromResult(result);
    }

    /// <summary>
    /// Confirms that an actual task checkbox changed while the native parent was embedded.
    /// </summary>
    public DesktopHostResult MarkInteractionVerified()
    {
        if (_embeddedParent == IntPtr.Zero)
        {
            return _lastResult;
        }

        if (!IsWindow(_embeddedParent))
        {
            return _lastResult = EnsureWidgetVisible(
                _window ?? throw new InvalidOperationException("The host window is not available."),
                "嵌入父窗口已失效，已回退为普通 Widget。",
                0);
        }

        _interactionVerified = true;
        _lastResult = new DesktopHostResult(
            DesktopHostMode.Embedded,
            $"已附着到 {_parentKind}，并观察到复选框状态真实变化；嵌入交互验证通过。",
            _parentKind,
            _lastResult.Attempts);
        return _lastResult;
    }

    /// <summary>
    /// Releases shell/display hooks. It does not terminate Explorer or any other process.
    /// </summary>
    public void Dispose()
    {
        StopMonitoring();
        if (_window is not null && _embeddedParent != IntPtr.Zero)
        {
            try
            {
                EnsureWidgetVisible(_window, "宿主释放时恢复普通 Widget。", 0);
            }
            catch
            {
                // The close path must not turn a best-effort cleanup into a crash.
            }
        }
    }

    private void EnsureSnapshot(Window window)
    {
        _window = window;
        if (_hasSnapshot)
        {
            return;
        }

        _windowHandle = new WindowInteropHelper(window).EnsureHandle();
        _originalParent = GetParent(_windowHandle);
        _originalOwner = GetWindow(_windowHandle, GwOwner);
        _originalStyle = GetWindowLongPtr(_windowHandle, GwlStyle).ToInt64();
        _originalExStyle = GetWindowLongPtr(_windowHandle, GwlExStyle).ToInt64();
        CaptureWidgetBounds(window);
        _hasSnapshot = true;
    }

    private void StartMonitoring(Window window)
    {
        EnsureSnapshot(window);
        if (_source is not null)
        {
            return;
        }

        _source = HwndSource.FromHwnd(_windowHandle);
        if (_source is not null)
        {
            _hook = WindowHook;
            _source.AddHook(_hook);
        }

        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    private void StopMonitoring()
    {
        if (_source is not null && _hook is not null)
        {
            _source.RemoveHook(_hook);
        }

        _source = null;
        _hook = null;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_window is null || _embeddedParent != IntPtr.Zero)
        {
            return;
        }

        if (e.PropertyName is nameof(SystemParameters.WorkArea)
            or nameof(SystemParameters.PrimaryScreenWidth)
            or nameof(SystemParameters.PrimaryScreenHeight))
        {
            var result = EnsureWidgetVisible(_window, "显示区域变化后已恢复到可见区域。", 0);
            PublishStatus(result);
        }
    }

    private IntPtr WindowHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            ScheduleRecovery("Explorer/TaskbarCreated");
        }
        else if (message is WmDisplayChange or WmDpiChanged or WmSettingChange)
        {
            ScheduleRecovery("显示设置或 DPI 变化");
        }

        return IntPtr.Zero;
    }

    private void ScheduleRecovery(string trigger)
    {
        if (_window is null || _recoveryPending)
        {
            return;
        }

        _recoveryPending = true;
        _ = RecoverAsync(_window, trigger);
    }

    private async Task RecoverAsync(Window window, string trigger)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            VerifyUiThread(window);

            if (_embeddedParent != IntPtr.Zero)
            {
                if (!IsWindow(_embeddedParent))
                {
                    var result = await AttachAsync(window, CancellationToken.None);
                    PublishStatus(result);
                }
                else
                {
                    var result = PositionEmbeddedWindow(window);
                    PublishStatus(result);
                }
            }
            else
            {
                PublishStatus(EnsureWidgetVisible(window, $"{trigger} 后恢复普通 Widget 可见区域。", 0));
            }
        }
        catch (Exception exception)
        {
            var result = EnsureWidgetVisible(
                window,
                $"{trigger} 恢复失败：{exception.Message} 已回退为普通 Widget。",
                0);
            PublishStatus(result);
        }
        finally
        {
            _recoveryPending = false;
        }
    }

    private void PublishStatus(DesktopHostResult result)
    {
        _lastResult = result;
        StatusChanged?.Invoke(this, new DesktopHostStatusChangedEventArgs(result));
    }

}
