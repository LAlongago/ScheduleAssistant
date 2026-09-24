using ScheduleAssistant.Application.Reminders;

namespace ScheduleAssistant.Infrastructure.Notifications;

/// <summary>Parses only the notification action and a canonical, non-empty task GUID.</summary>
internal static class NotificationActivationParser
{
    private const int MaximumArgumentLength = 512;

    public static NotificationActivationEventArgs Parse(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument) || argument.Length > MaximumArgumentLength)
        {
            return new NotificationActivationEventArgs(null);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var pair in argument.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    return new NotificationActivationEventArgs(null);
                }

                var key = Uri.UnescapeDataString(pair[..separator]);
                var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
                if (key is not ("action" or "taskId") || !values.TryAdd(key, value))
                {
                    return new NotificationActivationEventArgs(null);
                }
            }
        }
        catch (UriFormatException)
        {
            return new NotificationActivationEventArgs(null);
        }

        if (values.Count != 2
            || !values.TryGetValue("action", out var action)
            || !string.Equals(action, "openTask", StringComparison.Ordinal)
            || !values.TryGetValue("taskId", out var taskIdText)
            || !Guid.TryParseExact(taskIdText, "D", out var taskId)
            || taskId == Guid.Empty)
        {
            return new NotificationActivationEventArgs(null);
        }

        return new NotificationActivationEventArgs(taskId);
    }
}
