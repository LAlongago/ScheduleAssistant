namespace ScheduleAssistant.Application.Reminders;

/// <summary>Creates timer instances whose callbacks run at most once per scheduled delay.</summary>
public interface IOneShotTimerFactory
{
    /// <summary>Creates a disabled one-shot timer that invokes <paramref name="callback"/> when scheduled.</summary>
    IOneShotTimer Create(Func<Task> callback);
}

/// <summary>A controllable, single-delay timer used by the reminder scheduler.</summary>
public interface IOneShotTimer : IDisposable
{
    /// <summary>Schedules one callback after the supplied non-negative delay.</summary>
    void Schedule(TimeSpan delay);

    /// <summary>Cancels the currently scheduled callback.</summary>
    void CancelScheduledCallback();
}
