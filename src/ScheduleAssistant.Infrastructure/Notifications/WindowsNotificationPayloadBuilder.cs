using System.Globalization;
using System.Xml;
using ScheduleAssistant.Application.Reminders;

namespace ScheduleAssistant.Infrastructure.Notifications;

/// <summary>Builds the bounded, task-only content and actions shown in a native notification.</summary>
internal static class WindowsNotificationPayloadBuilder
{
    private const string ApplicationName = "ScheduleAssistant";
    private const string OpenTaskAction = "openTask";

    public static string Build(ReminderNotification reminder, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var taskId = reminder.TaskId.ToString("D", CultureInfo.InvariantCulture);
        var launchArguments = $"action={OpenTaskAction}&taskId={taskId}";
        using var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
        using (var writer = XmlWriter.Create(stringWriter, new XmlWriterSettings
        {
            ConformanceLevel = ConformanceLevel.Document,
            OmitXmlDeclaration = true
        }))
        {
            writer.WriteStartElement("toast");
            writer.WriteAttributeString("launch", launchArguments);
            writer.WriteStartElement("visual");
            writer.WriteStartElement("binding");
            writer.WriteAttributeString("template", "ToastGeneric");
            WriteText(writer, ApplicationName);
            WriteText(writer, reminder.TaskTitle);
            WriteText(writer, FormatDeadline(reminder, timeProvider.GetUtcNow().ToUniversalTime()));
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("actions");
            writer.WriteStartElement("action");
            writer.WriteAttributeString("content", "查看任务");
            writer.WriteAttributeString("arguments", launchArguments);
            writer.WriteAttributeString("activationType", "foreground");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        return stringWriter.ToString();
    }

    private static void WriteText(XmlWriter writer, string value)
    {
        writer.WriteStartElement("text");
        writer.WriteString(value);
        writer.WriteEndElement();
    }

    private static string FormatDeadline(ReminderNotification reminder, DateTimeOffset nowUtc)
    {
        var deadlineUtc = reminder.DeadlineUtc.ToUniversalTime();
        var localDeadline = deadlineUtc.ToLocalTime().ToString("yyyy年M月d日 HH:mm", CultureInfo.CurrentCulture);
        var remaining = deadlineUtc - nowUtc;
        if (reminder.IsDeadlineOverdue || remaining <= TimeSpan.Zero)
        {
            return reminder.IsDelayed
                ? $"Deadline：{localDeadline} · 已逾期 · 提醒时间已过"
                : $"Deadline：{localDeadline} · 已逾期";
        }

        var remainingText = FormatRemaining(remaining);
        return reminder.IsDelayed
            ? $"Deadline：{localDeadline} · 剩余{remainingText} · 提醒时间已过"
            : $"Deadline：{localDeadline} · 剩余{remainingText}";
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        var totalMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        if (totalMinutes >= 1_440)
        {
            var days = totalMinutes / 1_440;
            var hours = (totalMinutes % 1_440) / 60;
            return hours == 0 ? $"{days}天" : $"{days}天{hours}小时";
        }

        if (totalMinutes >= 60)
        {
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return minutes == 0 ? $"{hours}小时" : $"{hours}小时{minutes}分钟";
        }

        return $"{totalMinutes}分钟";
    }
}
