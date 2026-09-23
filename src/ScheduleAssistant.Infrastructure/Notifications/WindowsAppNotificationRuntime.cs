using Microsoft.Windows.AppNotifications;

namespace ScheduleAssistant.Infrastructure.Notifications;

/// <summary>Thin wrapper over the Windows App SDK AppNotificationManager singleton.</summary>
internal sealed class WindowsAppNotificationRuntime : IWindowsAppNotificationRuntime
{
    private AppNotificationManager? _manager;
    private bool _registered;

    /// <inheritdoc />
    public event Action<string?>? NotificationInvoked;

    /// <inheritdoc />
    public bool IsSupported() => AppNotificationManager.IsSupported();

    /// <inheritdoc />
    public AppNotificationSetting Setting => GetManager().Setting;

    /// <inheritdoc />
    public void Register()
    {
        if (_registered)
        {
            return;
        }

        var manager = GetManager();
        manager.NotificationInvoked += OnNotificationInvoked;
        try
        {
            manager.Register();
            _registered = true;
        }
        catch
        {
            manager.NotificationInvoked -= OnNotificationInvoked;
            throw;
        }
    }

    /// <inheritdoc />
    public void Unregister()
    {
        if (!_registered || _manager is null)
        {
            return;
        }

        var manager = _manager;
        manager.NotificationInvoked -= OnNotificationInvoked;
        _registered = false;
        manager.Unregister();
    }

    /// <inheritdoc />
    public void Show(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        GetManager().Show(new AppNotification(payload));
    }

    private AppNotificationManager GetManager() => _manager ??= AppNotificationManager.Default;

    private void OnNotificationInvoked(
        AppNotificationManager sender,
        AppNotificationActivatedEventArgs args)
    {
        _ = sender;
        NotificationInvoked?.Invoke(args.Argument);
    }
}
