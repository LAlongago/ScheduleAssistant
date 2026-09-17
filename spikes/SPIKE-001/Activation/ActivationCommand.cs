namespace ScheduleAssistant.Spike001.Notifications.Activation;

internal sealed record ActivationCommand(string Action, string? TaskId, string Source)
{
    public const string ShowWindowAction = "showWindow";
    public const string OpenTaskAction = "openTask";
}

internal static class ActivationArguments
{
    public static bool LooksLikeNotificationActivation(IEnumerable<string> arguments)
    {
        return arguments.Any(argument =>
            argument.Contains("AppNotificationActivated", StringComparison.OrdinalIgnoreCase));
    }

    public static ActivationCommand? ParseNotificationArgument(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in argument.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = pair.IndexOf('=');
            string key = separator >= 0 ? pair[..separator] : pair;
            string value = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
            values[key] = Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        values.TryGetValue("action", out string? action);
        values.TryGetValue("taskId", out string? taskId);

        return string.IsNullOrWhiteSpace(action)
            ? null
            : new ActivationCommand(action, taskId, "notification");
    }

    public static bool IsSupported(ActivationCommand command, string simulatedTaskId)
    {
        return command.Action switch
        {
            ActivationCommand.ShowWindowAction => string.IsNullOrWhiteSpace(command.TaskId),
            ActivationCommand.OpenTaskAction => string.Equals(
                command.TaskId,
                simulatedTaskId,
                StringComparison.Ordinal),
            _ => false,
        };
    }
}
