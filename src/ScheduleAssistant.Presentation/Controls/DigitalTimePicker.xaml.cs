using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ScheduleAssistant.Presentation.Controls;

/// <summary>
/// Selects an optional 24-hour wall-clock time as four independent digits while exposing the
/// existing editor-friendly <c>HH:mm</c> text contract.
/// </summary>
public partial class DigitalTimePicker : UserControl
{
    private const string EmptyDigit = "—";

    private static readonly string[] HourTensOptions = CreateOptions(2);
    private static readonly string[] MinuteTensOptions = CreateOptions(5);
    private static readonly string[] OnesOptions = CreateOptions(9);

    /// <summary>Identifies the two-way wall-clock text dependency property.</summary>
    public static readonly DependencyProperty TimeTextProperty = DependencyProperty.Register(
        nameof(TimeText),
        typeof(string),
        typeof(DigitalTimePicker),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTimeTextChanged));

    private bool _isSynchronizing;

    /// <summary>Initializes the four-digit time selector.</summary>
    public DigitalTimePicker()
    {
        InitializeComponent();
        HourTensSelector.ItemsSource = HourTensOptions;
        MinuteTensSelector.ItemsSource = MinuteTensOptions;
        MinuteOnesSelector.ItemsSource = OnesOptions;
        ApplyTimeText(string.Empty);
    }

    /// <summary>
    /// Gets or sets an optional wall-clock value in <c>HH:mm</c> form. A partially selected value
    /// remains intentionally invalid so the editor can surface its normal validation message.
    /// </summary>
    public string TimeText
    {
        get => (string)GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value ?? string.Empty);
    }

    private static void OnTimeTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var picker = (DigitalTimePicker)dependencyObject;
        if (!picker._isSynchronizing)
        {
            picker.ApplyTimeText(args.NewValue as string ?? string.Empty);
        }
    }

    private void ApplyTimeText(string value)
    {
        _isSynchronizing = true;
        try
        {
            if (!TryParseTime(value, out var time))
            {
                HourTensSelector.SelectedItem = EmptyDigit;
                SetHourOnesOptions(EmptyDigit);
                MinuteTensSelector.SelectedItem = EmptyDigit;
                MinuteOnesSelector.SelectedItem = EmptyDigit;
                return;
            }

            var hour = time.Hour.ToString("D2", CultureInfo.InvariantCulture);
            var minute = time.Minute.ToString("D2", CultureInfo.InvariantCulture);
            HourTensSelector.SelectedItem = hour[..1];
            SetHourOnesOptions(hour[1..]);
            MinuteTensSelector.SelectedItem = minute[..1];
            MinuteOnesSelector.SelectedItem = minute[1..];
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void OnDigitSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizing)
        {
            return;
        }

        if (ReferenceEquals(sender, HourTensSelector))
        {
            var previousHourOnes = HourOnesSelector.SelectedItem as string ?? EmptyDigit;
            _isSynchronizing = true;
            try
            {
                SetHourOnesOptions(previousHourOnes);
            }
            finally
            {
                _isSynchronizing = false;
            }
        }

        UpdateTimeTextFromDigits();
    }

    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        _isSynchronizing = true;
        try
        {
            HourTensSelector.SelectedItem = EmptyDigit;
            SetHourOnesOptions(EmptyDigit);
            MinuteTensSelector.SelectedItem = EmptyDigit;
            MinuteOnesSelector.SelectedItem = EmptyDigit;
        }
        finally
        {
            _isSynchronizing = false;
        }

        SetTimeText(string.Empty);
        HourTensSelector.Focus();
    }

    private void SetHourOnesOptions(string requestedSelection)
    {
        var maximum = HourTensSelector.SelectedItem as string == "2" ? 3 : 9;
        var options = maximum == 9 ? OnesOptions : CreateOptions(maximum);
        HourOnesSelector.ItemsSource = options;
        HourOnesSelector.SelectedItem = options.Contains(requestedSelection, StringComparer.Ordinal)
            ? requestedSelection
            : EmptyDigit;
    }

    private void UpdateTimeTextFromDigits()
    {
        var digits = new[]
        {
            HourTensSelector.SelectedItem as string,
            HourOnesSelector.SelectedItem as string,
            MinuteTensSelector.SelectedItem as string,
            MinuteOnesSelector.SelectedItem as string
        };
        if (digits.All(IsEmptyDigit))
        {
            SetTimeText(string.Empty);
            return;
        }

        var text = string.Concat(
            NormalizeDigit(digits[0]),
            NormalizeDigit(digits[1]),
            ":",
            NormalizeDigit(digits[2]),
            NormalizeDigit(digits[3]));
        SetTimeText(text);
    }

    private void SetTimeText(string value)
    {
        _isSynchronizing = true;
        try
        {
            SetCurrentValue(TimeTextProperty, value);
            GetBindingExpression(TimeTextProperty)?.UpdateSource();
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private static bool TryParseTime(string value, out TimeOnly time)
    {
        return TimeOnly.TryParseExact(
                   value,
                   "HH:mm",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out time)
            || TimeOnly.TryParse(
                value,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out time);
    }

    private static string[] CreateOptions(int maximum)
    {
        return new[] { EmptyDigit }
            .Concat(Enumerable.Range(0, maximum + 1).Select(value => value.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static bool IsEmptyDigit(string? value)
    {
        return string.IsNullOrEmpty(value) || string.Equals(value, EmptyDigit, StringComparison.Ordinal);
    }

    private static string NormalizeDigit(string? value)
    {
        return IsEmptyDigit(value) ? "-" : value!;
    }
}
