using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ScheduleAssistant.Spike002;

/// <summary>
/// In-memory task used only by SPIKE-002; it is deliberately not a domain entity.
/// </summary>
public sealed class SimulatedTask : INotifyPropertyChanged
{
    private bool _isCompleted;

    /// <summary>
    /// Initializes a simulated task.
    /// </summary>
    public SimulatedTask(string title, DateTime deadlineLocal)
    {
        Title = title;
        DeadlineLocal = deadlineLocal;
    }

    /// <summary>
    /// Raised when a property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when the completion checkbox changes.
    /// </summary>
    public event EventHandler? CompletionChanged;

    /// <summary>
    /// Gets the simulated title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the simulated local deadline.
    /// </summary>
    public DateTime DeadlineLocal { get; }

    /// <summary>
    /// Gets the display text for the deadline.
    /// </summary>
    public string DeadlineText => DeadlineLocal.ToString("M月d日 HH:mm", CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets the one-time relative deadline label.
    /// </summary>
    public string DeadlineStateText
    {
        get
        {
            var remaining = DeadlineLocal - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                return "模拟 Deadline 已到期";
            }

            if (remaining.TotalDays >= 1)
            {
                return $"剩余 {Math.Ceiling(remaining.TotalDays):0} 天";
            }

            return $"剩余 {Math.Max(1, Math.Ceiling(remaining.TotalHours)):0} 小时";
        }
    }

    /// <summary>
    /// Gets or sets the simulated completion state.
    /// </summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted == value)
            {
                return;
            }

            _isCompleted = value;
            OnPropertyChanged();
            CompletionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
