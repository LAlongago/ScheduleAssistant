using Microsoft.Windows.AppNotifications;
using ScheduleAssistant.Application.Reminders;

namespace ScheduleAssistant.Infrastructure.Notifications;

/// <summary>Maps native API and user settings to stable provider capability diagnostics.</summary>
internal static class WindowsNotificationCapabilityMapper
{
    public static NotificationProviderCapability Map(
        bool isSupported,
        bool isRegistered,
        AppNotificationSetting setting,
        string? registrationErrorCode = null)
    {
        if (!isSupported)
        {
            return NotificationProviderCapability.Unavailable("notification.api-unsupported");
        }

        if (!isRegistered)
        {
            return NotificationProviderCapability.Unavailable(
                registrationErrorCode ?? "notification.not-registered");
        }

        return setting switch
        {
            AppNotificationSetting.Enabled => NotificationProviderCapability.Available(),
            AppNotificationSetting.DisabledForApplication => NotificationProviderCapability.Unavailable(
                "notification.disabled-for-application"),
            AppNotificationSetting.DisabledForUser => NotificationProviderCapability.Unavailable(
                "notification.disabled-for-user"),
            AppNotificationSetting.DisabledByGroupPolicy => NotificationProviderCapability.Unavailable(
                "notification.disabled-by-policy"),
            AppNotificationSetting.DisabledByManifest => NotificationProviderCapability.Unavailable(
                "notification.disabled-by-manifest"),
            AppNotificationSetting.Unsupported => NotificationProviderCapability.Unavailable(
                "notification.system-unsupported"),
            _ => NotificationProviderCapability.Unavailable("notification.setting-unknown")
        };
    }
}
