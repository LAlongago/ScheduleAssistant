namespace ScheduleAssistant.Domain;

/// <summary>
/// Reminder delivery state. Numeric values are DEV-010 internal choices and are not yet a frozen database contract.
/// </summary>
public enum ReminderStatus
{
    Pending = 0,
    Delivered = 1,
    Expired = 2,
    Cancelled = 3,
    Failed = 4
}
