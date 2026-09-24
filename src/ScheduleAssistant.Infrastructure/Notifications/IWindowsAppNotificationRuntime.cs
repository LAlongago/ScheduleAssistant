using Microsoft.Windows.AppNotifications;

namespace ScheduleAssistant.Infrastructure.Notifications;

/// <summary>Small seam around Windows App SDK notification APIs for deterministic adapter tests.</summary>
internal interface IWindowsAppNotificationRuntime
{
    /// <summary>Raised with the raw activation argument after the native notification is invoked.</summary>
    event Action<string?>? NotificationInvoked;

    /// <summary>Initializes the framework runtime without showing UI; self-contained builds need no bootstrap.</summary>
    bool TryInitialize(out int hresult);

    /// <summary>Releases this process's framework runtime dependency after notification registration ends.</summary>
    void Shutdown();

    /// <summary>Reports whether this process can call the Windows App SDK notification APIs.</summary>
    bool IsSupported();

    /// <summary>Gets the current system or user notification setting.</summary>
    AppNotificationSetting Setting { get; }

    /// <summary>Subscribes to the native event and registers the application.</summary>
    void Register();

    /// <summary>Unsubscribes and removes this process's active registration.</summary>
    void Unregister();

    /// <summary>Displays one XML payload through the Windows App SDK manager.</summary>
    void Show(string payload);
}
