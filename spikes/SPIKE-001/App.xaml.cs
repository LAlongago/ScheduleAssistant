using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using ScheduleAssistant.Spike001.Notifications.Activation;

namespace ScheduleAssistant.Spike001.Notifications;

public partial class App : Application, IDisposable
{
    internal const string SimulatedTaskId = "SPIKE-001-DEMO-TASK-42";
    private const string SimulatedTaskTitle = "SPIKE-001 通知激活演示任务";
    private const string WindowsAppSdkPackageVersion = "2.4.0";

    private readonly List<string> _pendingDiagnostics = [];
    private MainWindow? _mainWindow;
    private AppNotificationManager? _notificationManager;
    private SingleInstanceCoordinator? _singleInstance;
    private bool _notificationRegistered;
    private bool _apiSupported;
    private bool _apiSupportKnown;
    private int _activationCount;
    private int _secondaryActivationForwarded;
    private Task? _secondaryForwardTask;
    private Task? _notificationRegistrationTask;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            _singleInstance = new SingleInstanceCoordinator(OnPipeCommand, OnPipeError);
            bool notificationActivationHint = ActivationArguments.LooksLikeNotificationActivation(
                Environment.GetCommandLineArgs());

            if (!_singleInstance.IsPrimary)
            {
                StartSecondaryInstance(notificationActivationHint);
                return;
            }

            CreateAndShowMainWindow();
            _singleInstance.StartServer();
            StartNotificationRegistration();
            AddDiagnostic("单实例主进程已启动；命名 Mutex 和命名管道已就绪。");
        }
        catch (Exception exception)
        {
            AddDiagnostic($"启动诊断失败：{exception.GetType().Name}: {exception.Message}");
            EnsureWindowForStartupFailure();
        }
    }

    private void CreateAndShowMainWindow()
    {
        _mainWindow = new MainWindow(
            SendTestNotification,
            RefreshNotificationCapability,
            HideMainWindow,
            ExitApplication);

        MainWindow = _mainWindow;
        _mainWindow.Show();
        _mainWindow.Activate();

        _mainWindow.SetRuntimeDiagnostics(BuildRuntimeDiagnostics());
        FlushPendingDiagnostics();
        UpdateCapabilityDisplay();
    }

    private void EnsureWindowForStartupFailure()
    {
        if (_mainWindow is null)
        {
            CreateAndShowMainWindow();
        }
    }

    private void StartNotificationRegistration()
    {
        _notificationRegistrationTask ??= RegisterNotificationsAsync();
    }

    private async Task RegisterNotificationsAsync()
    {
        try
        {
            AppNotificationManager manager = await Task.Run(
                    () => AppNotificationManager.Default)
                .WaitAsync(TimeSpan.FromSeconds(5));

            _notificationManager = manager;
            manager.NotificationInvoked += OnNotificationInvoked;

            try
            {
                _apiSupported = await Task.Run(AppNotificationManager.IsSupported)
                    .WaitAsync(TimeSpan.FromSeconds(5));
                _apiSupportKnown = true;
            }
            catch (Exception exception)
            {
                AddDiagnostic($"通知能力查询失败：{exception.GetType().Name}: {exception.Message}");
            }

            await Task.Run(manager.Register).WaitAsync(TimeSpan.FromSeconds(5));
            _notificationRegistered = true;
            AddDiagnostic("AppNotificationManager.Register() 成功。");
        }
        catch (Exception exception)
        {
            _notificationRegistered = false;
            AddDiagnostic($"通知注册失败（应用继续运行）：{exception.GetType().Name}: {exception.Message}");
        }

        UpdateCapabilityDisplay();
    }

    private void RefreshNotificationCapability()
    {
        if (_notificationManager is null)
        {
            AddDiagnostic("通知管理器尚未创建。");
            UpdateCapabilityDisplay();
            return;
        }

        try
        {
            _apiSupported = AppNotificationManager.IsSupported();
            _apiSupportKnown = true;
            AddDiagnostic("已刷新通知能力和系统通知设置。");
        }
        catch (Exception exception)
        {
            AddDiagnostic($"刷新通知能力失败：{exception.GetType().Name}: {exception.Message}");
        }

        UpdateCapabilityDisplay();
    }

    private void SendTestNotification()
    {
        if (!_notificationRegistered || _notificationManager is null)
        {
            AddDiagnostic("发送失败：通知未注册；请先查看能力/注册状态。");
            UpdateCapabilityDisplay();
            return;
        }

        try
        {
            var notification = new AppNotificationBuilder()
                .AddArgument("action", ActivationCommand.OpenTaskAction)
                .AddArgument("taskId", SimulatedTaskId)
                .AddText("ScheduleAssistant SPIKE-001")
                .AddText($"模拟任务：{SimulatedTaskTitle}")
                .AddText($"点击后打开固定任务 ID：{SimulatedTaskId}")
                .BuildNotification();

            _notificationManager.Show(notification);
            AddDiagnostic($"测试通知已发送；固定任务 ID={SimulatedTaskId}。");
        }
        catch (Exception exception)
        {
            AddDiagnostic($"发送通知失败（应用继续运行）：{exception.GetType().Name}: {exception.Message}");
        }

        UpdateCapabilityDisplay();
    }

    private void OnNotificationInvoked(
        AppNotificationManager sender,
        AppNotificationActivatedEventArgs args)
    {
        try
        {
            ActivationCommand? command = ActivationArguments.ParseNotificationArgument(args.Argument);
            if (command is null)
            {
                AddDiagnostic("收到通知激活，但载荷没有可识别的 action 参数。");
                return;
            }

            if (_singleInstance?.IsPrimary == true)
            {
                Dispatcher.BeginInvoke(() => HandleActivation(command with { Source = "notification" }));
                return;
            }

            if (_singleInstance is not null && Interlocked.Exchange(ref _secondaryActivationForwarded, 1) == 0)
            {
                _secondaryForwardTask = _singleInstance.SendToPrimaryAsync(
                    command with { Source = "secondary-notification" });
            }
        }
        catch (Exception exception)
        {
            AddDiagnostic($"通知激活处理失败：{exception.GetType().Name}: {exception.Message}");
        }
    }

    private void OnPipeCommand(ActivationCommand command)
    {
        if (_singleInstance?.IsPrimary != true)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => HandleActivation(command with { Source = "pipe" }));
    }

    private void OnPipeError(Exception exception)
    {
        Dispatcher.BeginInvoke(() => AddDiagnostic(
            $"单实例管道诊断：{exception.GetType().Name}: {exception.Message}"));
    }

    private void HandleActivation(ActivationCommand command)
    {
        if (!ActivationArguments.IsSupported(command, SimulatedTaskId))
        {
            AddDiagnostic($"忽略不受支持的激活命令：action={command.Action}, taskId={command.TaskId ?? "<none>"}。");
            return;
        }

        if (_mainWindow is null)
        {
            return;
        }

        _activationCount++;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.SetActivationResult(
            command.Action == ActivationCommand.OpenTaskAction ? SimulatedTaskId : "<none>",
            command.Source,
            _activationCount,
            Environment.ProcessId);

        AddDiagnostic(
            $"通知/IPC 激活成功：source={command.Source}, taskId={command.TaskId ?? "<none>"}, " +
            $"businessInstance=1, processId={Environment.ProcessId}。");
    }

    private void StartSecondaryInstance(bool notificationActivationHint)
    {
        AddDiagnostic("检测到已有业务实例；本进程不创建第二个窗口。");

        if (notificationActivationHint)
        {
            StartNotificationRegistration();
            _ = CompleteSecondaryNotificationActivationAsync();
        }
        else
        {
            _ = ForwardShowAndExitAsync();
        }
    }

    private async Task CompleteSecondaryNotificationActivationAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1500)).ConfigureAwait(false);

            if (Interlocked.CompareExchange(ref _secondaryActivationForwarded, 0, 0) == 0 &&
                _singleInstance is not null)
            {
                _secondaryForwardTask = _singleInstance.SendToPrimaryAsync(
                    new ActivationCommand(ActivationCommand.ShowWindowAction, null, "secondary-timeout"));
            }

            if (_secondaryForwardTask is not null)
            {
                await _secondaryForwardTask.ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"SPIKE-001 secondary activation failed: {exception}");
        }
        finally
        {
            await Dispatcher.InvokeAsync(() => Shutdown(0));
        }
    }

    private async Task ForwardShowAndExitAsync()
    {
        try
        {
            if (_singleInstance is not null)
            {
                await _singleInstance.SendToPrimaryAsync(
                    new ActivationCommand(ActivationCommand.ShowWindowAction, null, "secondary-launch"))
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"SPIKE-001 secondary launch forwarding failed: {exception}");
        }
        finally
        {
            await Dispatcher.InvokeAsync(() => Shutdown(0));
        }
    }

    private void HideMainWindow()
    {
        _mainWindow?.Hide();
        AddDiagnostic("窗口已隐藏；进程和提醒注册仍保留。");
    }

    private void ExitApplication()
    {
        Shutdown(0);
    }

    private void AddDiagnostic(string message)
    {
        string line = $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {message}";

        if (_mainWindow is null)
        {
            _pendingDiagnostics.Add(line);
            return;
        }

        if (_mainWindow.Dispatcher.CheckAccess())
        {
            _mainWindow.AppendDiagnostic(line);
        }
        else
        {
            _mainWindow.Dispatcher.BeginInvoke(() => _mainWindow.AppendDiagnostic(line));
        }
    }

    private void FlushPendingDiagnostics()
    {
        if (_mainWindow is null)
        {
            return;
        }

        foreach (string diagnostic in _pendingDiagnostics)
        {
            _mainWindow.AppendDiagnostic(diagnostic);
        }

        _pendingDiagnostics.Clear();
    }

    private void UpdateCapabilityDisplay()
    {
        if (_mainWindow is null)
        {
            return;
        }

        string setting;
        try
        {
            setting = _notificationManager?.Setting.ToString() ?? "Unavailable";
        }
        catch (Exception exception)
        {
            setting = $"Error: {exception.GetType().Name}";
        }

        string supported = _apiSupportKnown ? _apiSupported.ToString() : "Unknown";
        string registration = _notificationRegistered ? "Registered" : "Not registered";
        _mainWindow.SetCapabilityStatus(
            $"API supported: {supported} | Register: {registration} | System setting: {setting}");
    }

    private static string BuildRuntimeDiagnostics()
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"OS: {RuntimeInformation.OSDescription}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"OS version: {Environment.OSVersion.VersionString}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Runtime: {RuntimeInformation.FrameworkDescription}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Windows App SDK package: {WindowsAppSdkPackageVersion}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Elevation: {GetElevationState()}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Process ID: {Environment.ProcessId}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Executable: {Environment.ProcessPath}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Command line: {Environment.CommandLine}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Simulated task ID: {SimulatedTaskId}");
        builder.AppendLine("Packaging: unpackaged WPF prototype");
        return builder.ToString();
    }

    private static string GetElevationState()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator)
            ? "Elevated/admin (notification API is unsupported)"
            : "Standard user";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_notificationRegistered && _notificationManager is not null)
        {
            try
            {
                _notificationManager.NotificationInvoked -= OnNotificationInvoked;
                _notificationManager.Unregister();
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"SPIKE-001 notification unregister failed: {exception}");
            }
        }

        if (_singleInstance is not null)
        {
            _singleInstance.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        GC.SuppressFinalize(this);
    }

    private bool _disposed;

    private void OnExit(object sender, ExitEventArgs e)
    {
        Dispose();
    }
}
