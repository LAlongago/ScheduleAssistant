using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScheduleAssistant.Spike002;

public sealed partial class DesktopHostService
{
    private DesktopHostResult EnsureWidgetVisible(Window window, string reason, int attempts = 0)
    {
        try
        {
            VerifyUiThread(window);
            EnsureSnapshot(window);
            TryRestoreWidget(window);

            var workArea = GetWorkAreaInDips(window);
            var bounds = _widgetBounds ?? new Rect(96, 96, window.Width, window.Height);
            var width = Math.Min(Math.Max(320, bounds.Width), Math.Max(320, workArea.Width));
            var height = Math.Min(Math.Max(230, bounds.Height), Math.Max(230, workArea.Height));
            var left = Math.Clamp(bounds.Left, workArea.Left, workArea.Right - width);
            var top = Math.Clamp(bounds.Top, workArea.Top, workArea.Bottom - height);

            window.WindowState = WindowState.Normal;
            window.Left = left;
            window.Top = top;
            window.Width = width;
            window.Height = height;
            if (!window.IsVisible)
            {
                window.Show();
            }

            ShowWindow(_windowHandle, SwShownoactivate);
            _parentKind = "none";
            _lastResult = new DesktopHostResult(
                DesktopHostMode.WidgetFallback,
                reason,
                "none",
                attempts);
            return _lastResult;
        }
        catch (Exception exception)
        {
            _lastResult = new DesktopHostResult(
                DesktopHostMode.Unavailable,
                $"无法恢复为可见 Widget：{exception.Message}",
                _parentKind,
                attempts);
            return _lastResult;
        }
    }

    private void ApplyEmbeddedWindow(Window window, DesktopParent parent)
    {
        var previousParent = SetParentChecked(_windowHandle, parent.Handle);
        _embeddedParent = parent.Handle;
        _parentKind = parent.Kind;

        try
        {
            var style = GetWindowLongPtr(_windowHandle, GwlStyle).ToInt64();
            var embeddedStyle = (style | WsChild | WsVisible)
                & ~(long)(WsPopup | WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
            SetWindowLongPtrChecked(_windowHandle, GwlStyle, embeddedStyle);

            var exStyle = GetWindowLongPtr(_windowHandle, GwlExStyle).ToInt64();
            SetWindowLongPtrChecked(_windowHandle, GwlExStyle, exStyle & ~(long)(WsExAppWindow | WsExWindowEdge));
            PositionEmbeddedWindow(window);
        }
        catch
        {
            _embeddedParent = previousParent;
            throw;
        }
    }

    private DesktopHostResult PositionEmbeddedWindow(Window window)
    {
        if (_embeddedParent == IntPtr.Zero || !IsWindow(_embeddedParent))
        {
            return EnsureWidgetVisible(window, "嵌入父窗口无效，已回退为普通 Widget。", 0);
        }

        var client = new RectInt();
        if (!GetClientRect(_embeddedParent, ref client))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取桌面父窗口客户区。");
        }

        var size = GetWindowPixelSize(window);
        var x = Math.Min(32, Math.Max(0, client.Right - client.Left - size.Width));
        var y = Math.Min(32, Math.Max(0, client.Bottom - client.Top - size.Height));
        SetWindowPosChecked(
            _windowHandle,
            HwndBottom,
            x,
            y,
            size.Width,
            size.Height,
            SwpNoActivate | SwpNoOwnerZOrder | SwpFrameChanged | SwpShowWindow);
        ShowWindow(_windowHandle, SwShownoactivate);

        var mode = _interactionVerified
            ? DesktopHostMode.Embedded
            : DesktopHostMode.EmbeddedPendingInteraction;
        return _lastResult = new DesktopHostResult(
            mode,
            _interactionVerified
                ? $"已附着到 {_parentKind}，嵌入交互验证仍有效。"
                : $"已附着到 {_parentKind}；请勾选任一模拟任务验证真实输入。",
            _parentKind,
            _lastResult.Attempts);
    }

    private void TryRestoreWidget(Window window)
    {
        if (!_hasSnapshot)
        {
            return;
        }

        if (_embeddedParent != IntPtr.Zero || GetParent(_windowHandle) != _originalParent)
        {
            SetParentChecked(_windowHandle, _originalParent);
        }

        SetWindowLongPtrChecked(_windowHandle, GwlStyle, _originalStyle);
        SetWindowLongPtrChecked(_windowHandle, GwlExStyle, _originalExStyle);
        SetWindowLongPtrChecked(_windowHandle, GwlHwndParent, _originalOwner);
        _embeddedParent = IntPtr.Zero;
        _interactionVerified = false;
        _parentKind = "none";

        if (_widgetBounds is null)
        {
            CaptureWidgetBounds(window);
        }
    }

    private void CaptureWidgetBounds(Window window)
    {
        var left = double.IsNaN(window.Left) ? 96 : window.Left;
        var top = double.IsNaN(window.Top) ? 96 : window.Top;
        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        _widgetBounds = new Rect(left, top, width, height);
    }

    private static DesktopParent DiscoverDesktopParent()
    {
        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            return new DesktopParent(IntPtr.Zero, "none", "Progman 窗口不存在。");
        }

        _ = SendMessageTimeout(
            progman,
            ProgmanWorkerMessage,
            IntPtr.Zero,
            IntPtr.Zero,
            SmtoNormal,
            1000,
            out _);

        var worker = IntPtr.Zero;
        EnumWindows(
            (top, _) =>
            {
                var shellView = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView == IntPtr.Zero)
                {
                    return true;
                }

                worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                return worker == IntPtr.Zero;
            },
            IntPtr.Zero);

        if (worker != IntPtr.Zero && IsWindow(worker))
        {
            return new DesktopParent(worker, "WorkerW", "WorkerW 已发现。");
        }

        if (IsWindow(progman))
        {
            return new DesktopParent(progman, "Progman fallback", "WorkerW 未发现，尝试 Progman 兼容路径。");
        }

        return new DesktopParent(IntPtr.Zero, "none", "WorkerW 与 Progman 均不可用。");
    }

    private Rect GetWorkAreaInDips(Window window)
    {
        var fallback = SystemParameters.WorkArea;
        var monitor = MonitorFromWindow(_windowHandle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return fallback;
        }

        var dpi = GetDpiForWindow(_windowHandle);
        var scale = dpi == 0 ? 1.0 : dpi / 96.0;
        return new Rect(
            info.Work.Left / scale,
            info.Work.Top / scale,
            (info.Work.Right - info.Work.Left) / scale,
            (info.Work.Bottom - info.Work.Top) / scale);
    }

    private PixelSize GetWindowPixelSize(Window window)
    {
        var rect = new RectInt();
        if (GetWindowRect(_windowHandle, ref rect))
        {
            return new PixelSize(
                Math.Max(1, rect.Right - rect.Left),
                Math.Max(1, rect.Bottom - rect.Top));
        }

        var dpi = GetDpiForWindow(_windowHandle);
        var scale = dpi == 0 ? 1.0 : dpi / 96.0;
        return new PixelSize(
            Math.Max(1, (int)Math.Round(window.ActualWidth * scale)),
            Math.Max(1, (int)Math.Round(window.ActualHeight * scale)));
    }

    private static void VerifyUiThread(Window window)
    {
        if (!window.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException("Desktop host window operations must run on the WPF Dispatcher thread.");
        }
    }

    private static IntPtr SetParentChecked(IntPtr child, IntPtr parent)
    {
        Marshal.SetLastPInvokeError(0);
        var previous = SetParent(child, parent);
        var error = Marshal.GetLastPInvokeError();
        if (previous == IntPtr.Zero && error != 0)
        {
            throw new Win32Exception(error, "SetParent failed.");
        }

        return previous;
    }

    private static void SetWindowLongPtrChecked(IntPtr window, int index, long value)
    {
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtr(window, index, new IntPtr(value));
        var error = Marshal.GetLastPInvokeError();
        if (previous == IntPtr.Zero && error != 0)
        {
            throw new Win32Exception(error, "SetWindowLongPtr failed.");
        }
    }

    private static void SetWindowPosChecked(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags)
    {
        if (!SetWindowPos(window, insertAfter, x, y, width, height, flags))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowPos failed.");
        }
    }

    private static IntPtr GetWindowLongPtr(IntPtr window, int index)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(window, index)
            : GetWindowLong32(window, index);
    }

    private readonly record struct DesktopParent(IntPtr Handle, string Kind, string Reason);

    private readonly record struct PixelSize(int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectInt
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public RectInt Monitor;
        public RectInt Work;
        public uint Flags;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr window, int index, IntPtr value);

    private static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(window, index, value)
            : SetWindowLong32(window, index, value);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindow(IntPtr window, int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr window, uint command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr window, ref RectInt rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr window, ref RectInt rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetDpiForWindow(IntPtr window);
}
