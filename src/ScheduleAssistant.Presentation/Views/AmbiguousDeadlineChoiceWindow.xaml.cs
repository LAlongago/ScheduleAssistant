using System.Windows;

namespace ScheduleAssistant.Presentation.Views;

/// <summary>One explicit local/UTC interpretation of an ambiguous Deadline.</summary>
public sealed record AmbiguousDeadlineChoice(
    DateTimeOffset Utc,
    string LocalText,
    string UtcText);

/// <summary>Lets the user choose one of two valid DST interpretations.</summary>
public partial class AmbiguousDeadlineChoiceWindow : Window
{
    /// <summary>Initializes the ambiguity choice window.</summary>
    public AmbiguousDeadlineChoiceWindow(
        DateOnly localDate,
        TimeOnly localTime,
        string timeZoneId,
        IReadOnlyList<DateTimeOffset> candidates)
    {
        ArgumentNullException.ThrowIfNull(timeZoneId);
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count != 2)
        {
            throw new ArgumentException("Exactly two ambiguous Deadline candidates are required.", nameof(candidates));
        }

        Choices = candidates
            .Select((candidate, index) => new AmbiguousDeadlineChoice(
                candidate,
                $"{localDate:yyyy-MM-dd} {localTime:HH\\:mm}（第 {index + 1} 个有效时刻）",
                $"对应 UTC：{candidate.UtcDateTime:yyyy-MM-dd HH\\:mm}Z"))
            .ToArray();
        LocalDescription = $"本机时区：{timeZoneId}";
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>Gets the choices displayed to the user.</summary>
    public IReadOnlyList<AmbiguousDeadlineChoice> Choices { get; }

    /// <summary>Gets the local time-zone context displayed to the user.</summary>
    public string LocalDescription { get; }

    /// <summary>Gets or sets the selected choice.</summary>
    public AmbiguousDeadlineChoice? SelectedChoice { get; set; }

    /// <summary>Gets the selected UTC instant after confirmation.</summary>
    public DateTimeOffset? SelectedUtc { get; private set; }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (SelectedChoice is null)
        {
            return;
        }

        SelectedUtc = SelectedChoice.Utc;
        DialogResult = true;
    }
}
