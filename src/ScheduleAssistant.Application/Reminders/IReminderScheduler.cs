namespace ScheduleAssistant.Application.Reminders;

/// <summary>Controls the persistent one-shot reminder scheduler lifecycle.</summary>
public interface IReminderScheduler
{
    /// <summary>Runs overdue compensation, then schedules the earliest pending reminder.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Re-reads the earliest pending reminder and resets the one-shot timer.</summary>
    Task RescheduleAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the timer while retaining pending reminder state.</summary>
    Task PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs overdue compensation and resumes one-shot scheduling.</summary>
    Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops and releases the scheduler timer.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
